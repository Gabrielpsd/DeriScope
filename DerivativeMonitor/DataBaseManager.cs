using Microsoft.Data.Sqlite;
using System.Globalization;

// the columns of database are being defined in portuguese because the data is being fetched from a Brazilian source (B3)
public class DataBaseManager
{
    private static readonly CultureInfo Br = new CultureInfo("pt-BR");
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private SqliteConnection connection;
    public void Initialize(string source)
    {
        
        if(source == null) throw new ArgumentNullException("source");

        this.connection = new SqliteConnection($"Data Source={source}");
        this.connection.Open();

        var command = this.connection.CreateCommand();

        command.CommandText = @"
        CREATE TABLE IF NOT EXISTS Options (
            Ticket TEXT NOT NULL,
            TipoOpcao TEXT NOT NULL,
            Codigo TEXT NOT NULL,
            ISIN TEXT,
            Especificacao TEXT,
            Vencimento TEXT,
            PrecoExercicio TEXT,
            Referencia TEXT,
            Protegida INTEGER,
            Estilo TEXT,
            Ativo integer not null default 1,
            CriadoEm TEXT NULL,
            DeletadoEm TEXT NULL,
            PRIMARY KEY (Ticket, Codigo)
        );

        CREATE INDEX IF NOT EXISTS idx_codigo ON Options(Codigo);
        ";


        command.ExecuteNonQuery();

        command.CommandText = @"
        CREATE TABLE IF NOT EXISTS OptionHistory (
                Ticket     TEXT NOT NULL,
                Codigo     TEXT NOT NULL,
                Parametro  TEXT NOT NULL,
                Valor      REAL NOT NULL,
                CriadoEm   TEXT NOT NULL
                    );
        CREATE INDEX IF NOT EXISTS idx_hist_series ON OptionHistory(Codigo, Parametro, CriadoEm);
        ";


        command.ExecuteNonQuery();
        connection.Close();
    }

    public void SaveOptions(string source, string ticket, Options options)
    {

        this.connection.Open();
        using var transaction = this.connection.BeginTransaction();
        // UPSERT
        var cmd = this.connection.CreateCommand();

        var taggedOptions = options.CallOptions.Select(o => (opt: o, tipo: "Call")).Concat(options.PutOptions.Select(o => (opt: o, tipo: "Put")));
        cmd.Transaction = transaction;

        cmd.CommandText = @"
            INSERT INTO Options 
            (Ticket, Codigo,TipoOpcao, ISIN, Especificacao, Vencimento, PrecoExercicio, Referencia, Protegida, Estilo,Ativo, CriadoEm, DeletadoEm)
            VALUES 
            ($ticket, $codigo, $tipo, $isin, $esp, $venc, $preco, $ref, $prot, $estilo,1, $now, null)
            ON CONFLICT(Ticket, Codigo) DO UPDATE SET
                TipoOpcao = excluded.TipoOpcao,
                ISIN = excluded.ISIN,
                Especificacao = excluded.Especificacao,
                Vencimento = excluded.Vencimento,
                PrecoExercicio = excluded.PrecoExercicio,
                Referencia = excluded.Referencia,
                Protegida = excluded.Protegida,
                Estilo = excluded.Estilo,
                CriadoEm = excluded.CriadoEm,
                DeletadoEm = excluded.DeletadoEm;
            ";

        foreach (var (opt, tipo) in taggedOptions)
        {
            cmd.Parameters.Clear();

            cmd.Parameters.AddWithValue("$ticket", ticket);
            cmd.Parameters.AddWithValue("$codigo", opt.Codigo);
            cmd.Parameters.AddWithValue("$isin", opt.ISIN);
            cmd.Parameters.AddWithValue("$esp", opt.Especificacao);
            cmd.Parameters.AddWithValue("$venc", opt.Vencimento.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("$preco", opt.PrecoExercicio.ToString("#,##0.000", Inv));
            cmd.Parameters.AddWithValue("$ref", opt.Referencia);
            cmd.Parameters.AddWithValue("$prot", opt.Protegida ? 1 : 0);
            cmd.Parameters.AddWithValue("$estilo", opt.Estilo);
            cmd.Parameters.AddWithValue("$tipo", tipo);
            cmd.Parameters.AddWithValue("$now", DateTime.Now);
            cmd.ExecuteNonQuery();
        }

        // DELETE missing
        foreach (var (opt, tipo) in taggedOptions)
        {

            var updateCmd = this.connection.CreateCommand();
            updateCmd.Transaction = transaction;

            var parameters = taggedOptions.Select((o, i) => $"$c{i}").ToList();

            updateCmd.CommandText = $@"
            UPDATE Options
            SET DeletadoEm = $now,
                Ativo = 0       
            WHERE Ticket = $ticket
            AND Codigo NOT IN ({string.Join(",", parameters)})
            ";

            updateCmd.Parameters.AddWithValue("$ticket", ticket);

            for (int i = 0; i < taggedOptions.Count(); i++)
            {

                updateCmd.Parameters.AddWithValue($"$c{i}", taggedOptions.ElementAt(i).opt.Codigo);
            }

            updateCmd.Parameters.AddWithValue("$now", DateTime.Now);
            updateCmd.ExecuteNonQuery();
        }

        transaction.Commit();
        this.connection.Close();
    }

    public Options GetData(AppConfig config)
    {
        var options = new Options();
        this.connection.Open();
        var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Ticket, TipoOpcao, Codigo, ISIN, Especificacao, Vencimento, PrecoExercicio, Referencia, Protegida, Estilo
            FROM Options
            WHERE Ticket = $ticket AND Ativo = 1
            Order by PrecoExercicio
        ";
        cmd.Parameters.AddWithValue("$ticket", config.Ticker);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var opt = new OptionData
            {
                Codigo = reader.GetString(2),
                ISIN = reader.IsDBNull(3) ? null : reader.GetString(3),
                Especificacao = reader.IsDBNull(4) ? null : reader.GetString(4),
                Vencimento = DateTime.Parse(reader.GetString(5)),
                PrecoExercicio = reader.IsDBNull(6) ? 0 : decimal.Parse(reader.GetString(6), Inv),
                Referencia = reader.IsDBNull(7) ? null : reader.GetString(7),
                Protegida = !reader.IsDBNull(8) && reader.GetInt32(8) == 1,
                Estilo = reader.IsDBNull(9) ? null : reader.GetString(9)
            };

            if (reader.GetString(1) == "Put")
            {
                Logger.Log($"Adding Put option: {opt.Codigo} || Preco: {opt.PrecoExercicio} || Vencimento: {opt.Vencimento}");
                options.PutOptions.Add(opt);
            }
            else
            {
                Logger.Log($"Adding Call option: {opt.Codigo} || Preco: {opt.PrecoExercicio} || Vencimento: {opt.Vencimento}");
                options.CallOptions.Add(opt);
            }
        }
        this.connection.Close();
        return options;
    }

    public void SaveOptionHistory(BackgroundOption backgroundOption)
    {
        this.connection.Open();
        var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO OptionHistory (Ticket, Codigo, Parametro, Valor, CriadoEm)
            VALUES ($ticket, $codigo, $parametro, $valor, $criadoEm)
        ";
        cmd.Parameters.AddWithValue("$ticket", backgroundOption.stock);
        cmd.Parameters.AddWithValue("$codigo", backgroundOption.optionTicker);
        cmd.Parameters.AddWithValue("$parametro", backgroundOption.parameter);
        cmd.Parameters.AddWithValue("$valor", backgroundOption.value);
        cmd.Parameters.AddWithValue("$criadoEm", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
        this.connection.Close();
    }

    // the first parameter represents the average value of the parameter, the second one represents the population variance
    // Both values are being used to identifies unsual moviments on the options negotiations
    public void GetOptionHistoryVariation(BackgroundOption backgroundOption, DateTime? cutOff)
    {
        this.connection.Open();
        var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            SELECT 
                  Ticket,
                  Codigo,
                  Count(*) as Count,
                  Avg(Valor) as Average,
                  AVG(Valor * Valor) - AVG(Valor) * AVG(Valor) as Variance
            FROM OptionHistory
            WHERE Ticket = $ticket
            AND Codigo = $codigo 
            AND Parametro = $parametro
            and criadoEm >= $cutoff
            ORDER BY CriadoEm DESC
        ";

        
        cmd.Parameters.AddWithValue("$ticket", backgroundOption.stock);
        cmd.Parameters.AddWithValue("$codigo", backgroundOption.optionTicker);
        cmd.Parameters.AddWithValue("$parametro", backgroundOption.parameter);
        cmd.Parameters.AddWithValue("$cutoff", cutOff);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            backgroundOption.average = double.Parse(reader.IsDBNull(3) ? "0" : reader.GetDouble(3).ToString("F4"));
            backgroundOption.variance = double.Parse(reader.IsDBNull(4) ? "0" : reader.GetDouble(4).ToString("F4"));
        }
        this.connection.Close();
    }
}