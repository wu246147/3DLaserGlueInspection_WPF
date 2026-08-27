using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MySql.Data.MySqlClient;

namespace _3DLaserGlueInspection
{
    public class wSql
    {
        MySqlConnection connection;
        public string Database { get; }

        public wSql(string server, string user_id, string password, string database)
        {
            Database = database;
            string s = $"server={server}; user id={user_id}; password={password}; database={database};";
            connection = new MySqlConnection(s);
        }
        public void Open()
        {
            connection?.Open();
        }
        public void Close()
        {
            connection?.Close();
        }

        MySqlTransaction mySqlTransaction;
        public void BeginTransaction()
        {
            mySqlTransaction = connection.BeginTransaction();
        }
        public void Commit()
        {
            mySqlTransaction?.Commit();
        }
        public void Rollback()
        {
            mySqlTransaction?.Rollback();
        }

        public int RunSql(string sqlQuery, out DataTable dataTable)
        {
            dataTable = new DataTable();
            MySqlCommand command = new MySqlCommand(sqlQuery, connection);
            MySqlDataAdapter adapt = new MySqlDataAdapter();
            adapt.SelectCommand = command;
            return adapt.Fill(dataTable);
        }

        /// <summary>
        /// 添加行
        /// </summary>
        /// <param name="tableName">表名</param>
        /// <param name="vs">列数据数组</param>
        /// <returns></returns>
        public int InsertRow(string tableName, string[] vs)
        {
            string str = "'" + vs[0] + "'";
            for (int i = 1; i < vs.Length; i++)
            {
                str += ",'" + vs[i] + "'";
            }
            string sqlQuery = $"INSERT INTO {tableName} VALUES({str})";
            MySqlCommand command = new MySqlCommand(sqlQuery, connection);
            int r = command.ExecuteNonQuery();
            return r;
        }

        public int InsertRow(string tableName, Dictionary<string, string> pairs)
        {
            int r = -1;
            if (pairs != null && pairs.Count > 0)
            {
                string[] keys = pairs.Keys.ToArray();
                string names = "" + keys[0] + "";
                string values = "'" + pairs[keys[0]] + "'";
                for (int i = 1; i < pairs.Count; i++)
                {
                    names += "," + keys[i] + "";
                    values += ",'" + pairs[keys[i]] + "'";
                }
                string sqlQuery = $"INSERT INTO {tableName} ({names}) VALUES({values})";
                MySqlCommand command = new MySqlCommand(sqlQuery, connection);
                r = command.ExecuteNonQuery();
            }
            return r;
        }
        public void Dispose()
        {
            connection?.Dispose();
        }
    }
}
