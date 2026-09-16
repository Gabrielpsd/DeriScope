using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Transactions;

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

        command.CommandText = @"
        CREATE TABLE IF NOT EXISTS TriggeredAllerts (
                Ticket     TEXT NOT NULL,
                Codigo     TEXT NOT NULL,
                Parametro  TEXT NOT NULL,
                Valor      REAL NOT NULL,
                CriadoEm   TEXT NOT NULL,
                Zscore      REAL NOT NULL,
                Message     TEXT NOT NULL,
                Media     REAL NOT NULL
                    );
        CREATE INDEX IF NOT EXISTS idx_triggered_alerts ON TriggeredAllerts(Codigo, Parametro, CriadoEm);
        ";

        command.ExecuteNonQuery();

        command.CommandText = @"
        CREATE TABLE IF NOT EXISTS lastSuccessfullWebScrapping (
                Ticket     TEXT NOT NULL,
                criadoEm   TEXT NOT NULL,
                PRIMARY KEY (Ticket)
            );

            CREATE INDEX IF NOT EXISTS idx_last_successfull_scrapping ON lastSuccessfullWebScrapping(Ticket);
        ";

        command.ExecuteNonQuery();

        connection.Close();
    }

    public string getLastSuccessfullWebScrapping(string ticket)
    {
        this.connection.Open();
        var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            SELECT criadoEm
            FROM lastSuccessfullWebScrapping
            WHERE Ticket = $ticket
        ";
        cmd.Parameters.AddWithValue("$ticket", ticket);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            this.connection.Close();
            return reader.GetString(0);
        }
        else
        {
            this.connection.Close();
            return String.Empty;
        }
    }

    public void saveLastSuccessfullWebScrapping(string ticket, DateTime lastSuccessfullWebScrapping)
    {
        this.connection.Open();
        var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO lastSuccessfullWebScrapping (Ticket, criadoEm)
            VALUES ($ticket, $criadoEm)
            ON CONFLICT(Ticket) DO UPDATE SET
                criadoEm = excluded.criadoEm
        ";
        cmd.Parameters.AddWithValue("$ticket", ticket);
        cmd.Parameters.AddWithValue("$criadoEm", lastSuccessfullWebScrapping.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.ExecuteNonQuery();
        this.connection.Close();
    }

    public void SaveTriggeredAlert(Alert alert, string codigo, string parametro)
    {
        this.connection.Open();
        var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO TriggeredAllerts (Ticket, Codigo, Parametro, Valor, CriadoEm, Zscore, Message, Media)
            VALUES ($ticket, $codigo, $parametro, $valor, $criadoEm, $zscore, $message, $media)
        ";
        cmd.Parameters.AddWithValue("$ticket", alert.Ticker);
        cmd.Parameters.AddWithValue("$codigo", codigo);
        cmd.Parameters.AddWithValue("$parametro", parametro);
        cmd.Parameters.AddWithValue("$valor", alert.Value);
        cmd.Parameters.AddWithValue("$criadoEm", alert.Time.ToString("yyyy-MM-dd HH:mm:ss"));
        cmd.Parameters.AddWithValue("$zscore", alert.ZScore);
        cmd.Parameters.AddWithValue("$message", alert.Message);
        cmd.Parameters.AddWithValue("$media", alert.Average);
        cmd.ExecuteNonQuery();
        this.connection.Close();
    }

    public void SaveOption(string ticket,string codigo,string ISIN, string especificacao, DateTime vencimento, decimal precoExercicio, string referencia, bool protegida, string estilo, string tipo)
    {
        this.connection.Open();
        var cmd = this.connection.CreateCommand();
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
        cmd.Parameters.AddWithValue("$ticket", ticket);
        cmd.Parameters.AddWithValue("$codigo", codigo);
        cmd.Parameters.AddWithValue("$isin", ISIN);
        cmd.Parameters.AddWithValue("$esp", especificacao);
        cmd.Parameters.AddWithValue("$venc", vencimento.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("$preco", precoExercicio.ToString("#,##0.000", Inv));
        cmd.Parameters.AddWithValue("$ref", referencia);
        cmd.Parameters.AddWithValue("$prot", protegida ? 1 : 0);
        cmd.Parameters.AddWithValue("$estilo", estilo);
        cmd.Parameters.AddWithValue("$tipo", tipo);
        cmd.Parameters.AddWithValue("$now", DateTime.Now);
        cmd.ExecuteNonQuery();

        this.connection.Close();
    }

    public List<OptionData> GetoptionOndDatabaseData(string ticker, bool isCall)
    {
        var options = new List<OptionData>();

        var call = isCall ? "Call": "Put";

        this.connection.Open();
        var cmd = this.connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Ticket, TipoOpcao, Codigo, ISIN, Especificacao, Vencimento, PrecoExercicio, Referencia, Protegida, Estilo
            FROM Options
            WHERE Ticket = $ticket AND Ativo = 1 and TipoOpcao = $call
            Order by PrecoExercicio
        ";
        cmd.Parameters.AddWithValue("$ticket", ticker );
        cmd.Parameters.AddWithValue("$call", call);

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var opt = new OptionData
            {
                Codigo = reader.GetString(2),
                Vencimento = DateTime.Parse(reader.GetString(5)),
                PrecoExercicio = reader.IsDBNull(6) ? 0 : decimal.Parse(reader.GetString(6), Inv),
            };

            options.Add(opt);
            
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