using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace WinFail2Ban.Util
{
    public class SqliteUtil
    {
        private static readonly Object lockObj = new Object();
        public string InitTableSql { get; set; }
        public string ConnStr { get; set; }
        const string DB_FILE_NAME = "HackInfo.db";
        const string INIT_SQL = @"
                                CREATE TABLE History(
                                   Id Text,
                                   IpAddress VARCHAR(80),
                                   CreateDate INTEGER,
                                   EventSource VARCHAR(50),
                                   EventId INTEGER,
                                   RemoteWorkGroup VARCHAR(50)
                                );

                                CREATE TABLE BlackList(
                                    IpAddress VARCHAR(80),
                                    Reason TEXT,
                                    CreateDate INTEGER
                                ); 

                                CREATE TABLE WhiteList(
                                    IpAddress VARCHAR(80),
                                    Reason TEXT,
                                    CreateDate INTEGER,
                                    ExpiredDate INTEGER
                                ); 
                                
                                CREATE UNIQUE INDEX Idx_Id
                                on History(Id);

                                CREATE INDEX Idx_Ip
                                on History(IpAddress,EventId);
                                ";
        private SqliteUtil(string initTableSql)
        {
            if (!File.Exists(DbFilePath))
            {
                SQLiteConnection.CreateFile(DbFilePath);
                if (!string.IsNullOrEmpty(initTableSql))
                {
                    this.InitTableSql = initTableSql;
                    CreateTable();
                }
            }

        }
        private static SqliteUtil _instacne;
        private static string _dbFilePath;
        private SqliteUtil()
        {
        }



        public static SqliteUtil GetInstance(string dbFilePath=null,string initTableSql = null)
        {
            if (_instacne != null)
            {
                return _instacne;
            }
            else
            {
                if (!string.IsNullOrEmpty(dbFilePath) && File.Exists(dbFilePath))
                {
                    _dbFilePath = dbFilePath;
                }
                if (string.IsNullOrEmpty(initTableSql))
                {
                    _instacne = new SqliteUtil();
                }
                else
                {
                    _instacne = new SqliteUtil(initTableSql);
                }

                

                return _instacne;
            }
        }

        private string DbFilePath
        {
            get
            {
                if (!string.IsNullOrEmpty(_dbFilePath))
                {
                    return _dbFilePath;
                }
                else
                {
                    return DB_FILE_NAME;
                }
            }
        }
        public bool HasDbFile
        {
            get
            {
                return File.Exists(DbFilePath);
            }
        }

        public void Init()
        {
            if (!HasDbFile)
            {
                SQLiteConnection.CreateFile(DbFilePath);
            }
            if (!HasTables)
            {
                if(string.IsNullOrWhiteSpace(this.InitTableSql))
                {
                this.InitTableSql = INIT_SQL;
                }
                CreateTable();
            }

        }

        private int? _maxSizeRollBackups;

        private const int DEFAULT_MAX_SIZE_ROLL_BACKUPS = 3;
        private int MaxSizeRollBackups
        {
            get
            {
                if (!_maxSizeRollBackups.HasValue)
                {
                    string maxSize = System.Configuration.ConfigurationManager.AppSettings["MaxSizeRollBackups"];
                    if (string.IsNullOrEmpty(maxSize))
                    {
                        return DEFAULT_MAX_SIZE_ROLL_BACKUPS;
                    }
                    else
                    {
                        int size = -1;
                        int.TryParse(maxSize, out size);
                        if (size > 0)
                        {
                            _maxSizeRollBackups = size;
                            return size;
                        }
                        else
                        {
                            return DEFAULT_MAX_SIZE_ROLL_BACKUPS;
                        }
                    }
                }
                return _maxSizeRollBackups.Value;
            }

        }

        public void BackupDbFile()
        {
            FileInfo file = new FileInfo(DbFilePath);
            if (file.Exists)
            {
                lock (lockObj)
                {
                    List<FileInfo> fileInfos = file.Directory.GetFiles()
                       .Where(t => t.Name.StartsWith(file.Name) && t.Extension==".sqlite"  && t.Name!=file.Name)
                       .OrderByDescending(t =>
                       {
                           string[] array = t.Name.Split(new string[] { "." }, StringSplitOptions.RemoveEmptyEntries);
                           return long.Parse(array[array.Length-2]);
                       })
                       .ToList();
                    for (int i = 0; i < fileInfos.Count; i++)
                    {

                        if (i + 1 >= MaxSizeRollBackups)
                        {
                            fileInfos[i].Delete();
                        }

                    }
                    File.Copy(DbFilePath, $"{DbFilePath}.{DateTime.Now.ToString("yyyyMMddHHmmss")}.sqlite", true);
                }
            }
        }

        private void Connect(Action<SQLiteConnection> action)
        {
            string connStr = null;
            if (!string.IsNullOrEmpty(DbFilePath))
            {
                if (!File.Exists(DbFilePath))
                {
                    throw new Exception("Sqlite Db Not Found!");
                }
                SQLiteConnectionStringBuilder connectionStringBuilder = new SQLiteConnectionStringBuilder();
                connectionStringBuilder.DataSource = DbFilePath;
                connectionStringBuilder.JournalMode = SQLiteJournalModeEnum.Wal;
                connStr = connectionStringBuilder.ToString();
            }
            if (!string.IsNullOrEmpty(ConnStr))
            {
                connStr = ConnStr;
            }
            using (SQLiteConnection conn = new SQLiteConnection(connStr))
            {
                conn.Open();
                if (action != null)
                {
                    action.Invoke(conn);
                }
            }
        }

        public bool HasTables
        {
            get
            {
                bool result = false;
                ExecuteReader("select * from sqlite_master where type = 'table' order by name;", (reader) =>
                {
                    result = reader.HasRows;
                });
                return result;
            }
        }

        void CreateTable()
        {
            if (!string.IsNullOrWhiteSpace(InitTableSql))
            {
                Connect(conn =>
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(InitTableSql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                });
            }
        }

        public void ExecuteNoneQuery(string sql)
        {
            Connect(conn =>
            {
                lock (lockObj)
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(sql, conn))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            });
        }

        public void ExecuteNoneQuery(string sql, params SQLiteParameter[] parameters)
        {
            Connect(conn =>
            {
                lock (lockObj)
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(sql, conn))
                    {
                        if (parameters != null)
                        {
                            cmd.Parameters.AddRange(parameters);
                        }
                        cmd.ExecuteNonQuery();
                    }
                }
            });
        }

        public void ExecuteMulitLineNoneQuery(List<string> sql)
        {
            Connect(conn =>
            {
                lock (lockObj)
                {
                    using (var trans = conn.BeginTransaction())
                    {
                        try
                        {
                            using (SQLiteCommand cmd = new SQLiteCommand(String.Join(";", sql), conn))
                            {
                                cmd.ExecuteNonQuery();
                            }
                            trans.Commit();
                        }
                        catch (System.Exception ex)
                        {
                            trans.Rollback();
                            throw ex;
                        }
                    }
                }
            });
        }




        public void ExecuteReader(string sql, Action<SQLiteDataReader> action)
        {
            Connect(conn =>
            {
                lock (lockObj)
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(sql, conn))
                    {
                        using (SQLiteDataReader dr = cmd.ExecuteReader())
                        {
                            if (action != null)
                            {
                                action.Invoke(dr);
                            }
                        }
                    }
                }
            });
        }

        public void ExecuteReader<T>(string sql, Action<List<T>> action)
        {
            Connect(conn =>
            {
                lock (lockObj)
                {
                    using (SQLiteCommand cmd = new SQLiteCommand(sql, conn))
                    {
                        using (SQLiteDataReader dr = cmd.ExecuteReader())
                        {
                            if (action != null)
                            {
                                action.Invoke(SqlDataReaderToIList<T>(dr));
                            }
                        }
                    }
                }
            });
        }

        private static List<T> SqlDataReaderToIList<T>(SQLiteDataReader sqldatareader)
        {

            List<T> list = new List<T>();
            while (sqldatareader.Read())
            {
                T t = System.Activator.CreateInstance<T>();
                Type type = t.GetType();
                for (int i = 0; i < sqldatareader.FieldCount; i++)
                {
                    object TempValue = null;
                    if (sqldatareader.IsDBNull(i))
                    {
                        string typeFullName = type.GetProperty(sqldatareader.GetName(i)).PropertyType.FullName;
                        TempValue = GetDbNullValue(typeFullName);
                    }
                    else
                    {
                        TempValue = sqldatareader.GetValue(i);
                    }
                    type.GetProperty(sqldatareader.GetName(i)).SetValue(t, TempValue, null);
                }
                list.Add(t);


            }
            return list;
        }


        private static object GetDbNullValue(string typeFullName)
        {
            typeFullName = typeFullName.ToLower();
            if (typeFullName == "string")
                return string.Empty;
            else if (typeFullName == "int32" || typeFullName == "int16" || typeFullName == "int64")
                return 0;
            else if (typeFullName == "datetime")
                return Convert.ToDateTime(DateTime.MinValue);
            else if (typeFullName == "boolean")
                return false;
            else if (typeFullName == "int")
                return 0;
            return null;
        }

    }
}
