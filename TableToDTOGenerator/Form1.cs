using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TableToDTOGenerator
{
    public partial class Form1 : Form
    {
        #region 获取基本信息
        private static readonly string infoSql = @"SELECT
	                                                c.name AS column_name,																									
	                                                t.name AS data_type,
	                                                c.max_length length,
                                                    c.[precision],
													c.scale,
                                                    case WHEN c.is_nullable = 0 THEN 'true' else 'false' end required,
	                                                (
		                                                SELECT
		                                                VALUE
		                                                FROM
			                                                sys.extended_properties AS ex
		                                                WHERE
			                                                ex.major_id = c.object_id
		                                                AND ex.minor_id = c.column_id
	                                                ) AS notes
                                                FROM
	                                                sys.columns AS c
                                                INNER JOIN sys.tables AS ta ON c.object_id = ta.object_id
                                                INNER JOIN (
	                                                SELECT
		                                                name,
		                                                system_type_id
	                                                FROM
		                                                sys.types
	                                                WHERE
		                                                name <> 'sysname'
                                                ) AS t ON c.system_type_id = t.system_type_id
                                                WHERE
	                                                ta.name = '{0}'
                                                ORDER BY
	                                                c.column_id";

        public Form1()
        {
            InitializeComponent();
        }

        private string[] GetTableNames()
        {
            SqlConnection conn = null;
            try
            {
                conn = new SqlConnection(textBox2.Text);
                conn.Open();

                string[] restrictions = new string[4];
                List<string> tabNames = new List<string>();
                restrictions[0] = textBox3.Text;
                DataTable table = conn.GetSchema("Tables");

                conn.Close();

                foreach (System.Data.DataRow row in table.Rows)
                {
                    tabNames.Add(row.ItemArray[2].ToString());
                }

                return tabNames.ToArray<string>();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }
            finally
            {
                if (conn != null && conn.State == ConnectionState.Open)
                {
                    conn.Close();
                }
            }
        }

        private Dictionary<string, List<FiledInfo>> GetTableInfo(string[] tbnames)
        {
            Dictionary<string, List<FiledInfo>> ret = new Dictionary<string, List<FiledInfo>>();
            SqlConnection conn = null;
            try
            {
                conn = new SqlConnection(textBox2.Text);
                conn.Open();

                foreach (var item in tbnames)
                {
                    string sql = string.Format(infoSql, item);
                    SqlCommand cmd = new SqlCommand(sql, conn);
                    List<FiledInfo> filed = new List<FiledInfo>();
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            FiledInfo fi = new FiledInfo();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                fi.colName = reader["column_name"].ToString();
                                fi.dbType = reader["data_type"].ToString();
                                fi.colNote = reader["notes"].ToString();
                                fi.precision = reader["precision"].ToString();
                                fi.scale = reader["scale"].ToString();
                                fi.dbLength = reader["length"].ToString();
                                fi.required = reader["required"].ToString();
                            }
                            filed.Add(fi);
                        }
                    }
                    ret.Add(item, filed);
                }

                conn.Close();

                return ret;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }
            finally
            {
                if (conn != null && conn.State == ConnectionState.Open)
                {
                    conn.Close();
                }
            }
        }
        #endregion

        private void CreateDTO(Dictionary<string, List<FiledInfo>> tinfo)
        {
            string dir = System.IO.Path.Combine(System.Environment.CurrentDirectory, "DTO");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            StringBuilder sb = new StringBuilder();
            foreach (var tab in tinfo)
            {
                string fileName = Path.Combine(dir, tab.Key + "DTO.cs");
                using (StreamWriter sw = File.CreateText(fileName))
                {
                    //sb.AppendLine("");

                    sb.AppendLine("using System;");
                    sb.AppendLine("using System.Text;");
                    sb.AppendLine("using System.Collections.Generic;");
                    sb.AppendLine("using System.Data;");
                    sb.AppendLine("");
                    sb.AppendLine("namespace Framework.DTO");
                    sb.AppendLine("{");

                    sb.AppendLine("    [Serializable]");
                    sb.AppendLine(string.Format("    [TableInfo(TableName = \"{0}\")]", tab.Key));
                    sb.AppendLine(string.Format("    public class {0}DTO", tab.Key));
                    sb.AppendLine("    {");

                    foreach (var field in tab.Value)
                    {
                        sb.AppendLine("");
                        sb.AppendLine("        /// <summary>");
                        sb.AppendLine(string.Format("        /// {0}", field.colNote));
                        sb.AppendLine("        /// </summary>");

                        switch (field.dbType)
                        {
                            case "uniqueidentifier":
                                sb.AppendLine(string.Format("        [FieldInfo(DataFieldLength = {0},DataFieldPrecision = {3},DataFieldScale = {4},FiledName = \"{1}\",Required = {2},DataLength = {5})]", field.dbLength, field.colName, field.required, field.precision, field.scale, 32));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Guid", field.colName));
                                break;
                            case "nvarchar":
                                sb.AppendLine(string.Format("        [FieldInfo(DataFieldLength = {0},DataFieldPrecision = {3},DataFieldScale = {4},FiledName = \"{1}\",Required = {2},DataLength = {5})]", field.dbLength, field.colName, field.required, field.precision, field.scale, Convert.ToInt32(field.dbLength) / 2));
                                sb.AppendLine(string.Format("        public {0} {1}", "String", field.colName));
                                break;
                            case "varchar":
                                sb.AppendLine(string.Format("        [FieldInfo(DataFieldLength = {0},DataFieldPrecision = {3},DataFieldScale = {4},FiledName = \"{1}\",Required = {2},DataLength = {5})]", field.dbLength, field.colName, field.required, field.precision, field.scale, Convert.ToInt32(field.dbLength)));
                                sb.AppendLine(string.Format("        public {0} {1}", "String", field.colName));
                                break;
                            case "int":
                                sb.AppendLine(string.Format("        [FieldInfo(DataFieldLength = {0},DataFieldPrecision = {3},DataFieldScale = {4},FiledName = \"{1}\",Required = {2},DataLength = {5})]", field.dbLength, field.colName, field.required, field.precision, field.scale, Convert.ToInt32(field.dbLength)));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Int32", field.colName));
                                break;
                            case "datetime":
                                sb.AppendLine(string.Format("        [FieldInfo(DataFieldLength = {0},DataFieldPrecision = {3},DataFieldScale = {4},FiledName = \"{1}\",Required = {2},DataLength = {5})]", field.dbLength, field.colName, field.required, field.precision, field.scale, Convert.ToInt32(field.dbLength)));
                                sb.AppendLine(string.Format("        public {0}? {1}", "DateTime", field.colName));
                                break;
                            case "bit":
                                sb.AppendLine(string.Format("        [FieldInfo(DataFieldLength = {0},DataFieldPrecision = {3},DataFieldScale = {4},FiledName = \"{1}\",Required = {2},DataLength = {5})]", field.dbLength, field.colName, field.required, field.precision, field.scale, Convert.ToInt32(field.dbLength)));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Boolean", field.colName));
                                break;
                            case "tinyint":
                                sb.AppendLine(string.Format("        [FieldInfo(DataFieldLength = {0},DataFieldPrecision = {3},DataFieldScale = {4},FiledName = \"{1}\",Required = {2},DataLength = {5})]", field.dbLength, field.colName, field.required, field.precision, field.scale, Convert.ToInt32(field.dbLength)));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Byte", field.colName));
                                break;
                            case "smallint":
                                sb.AppendLine(string.Format("        [FieldInfo(DataFieldLength = {0},DataFieldPrecision = {3},DataFieldScale = {4},FiledName = \"{1}\",Required = {2},DataLength = {5})]", field.dbLength, field.colName, field.required, field.precision, field.scale, Convert.ToInt32(field.dbLength)));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Int16", field.colName));
                                break;
                            case "bigint":
                                sb.AppendLine(string.Format("        [FieldInfo(DataFieldLength = {0},DataFieldPrecision = {3},DataFieldScale = {4},FiledName = \"{1}\",Required = {2},DataLength = {5})]", field.dbLength, field.colName, field.required, field.precision, field.scale, Convert.ToInt32(field.dbLength)));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Int64", field.colName));
                                break;
                            case "real":
                                sb.AppendLine(string.Format("        [FieldInfo(DataFieldLength = {0},DataFieldPrecision = {3},DataFieldScale = {4},FiledName = \"{1}\",Required = {2},DataLength = {5})]", field.dbLength, field.colName, field.required, field.precision, field.scale, Convert.ToInt32(field.dbLength)));
                                sb.AppendLine(string.Format("        public {0} {1}", "float", field.colName));
                                break;
                            case "money":
                                sb.AppendLine(string.Format("        [FieldInfo(DataFieldLength = {0},DataFieldPrecision = {3},DataFieldScale = {4},FiledName = \"{1}\",Required = {2},DataLength = {5})]", field.dbLength, field.colName, field.required, field.precision, field.scale, Convert.ToInt32(field.dbLength)));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Decimal", field.colName));
                                break;
                            case "decimal":
                                sb.AppendLine(string.Format("        [FieldInfo(DataFieldLength = {0},DataFieldPrecision = {3},DataFieldScale = {4},FiledName = \"{1}\",Required = {2},DataLength = {5})]", field.dbLength, field.colName, field.required, field.precision, field.scale, Convert.ToInt32(field.dbLength)));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Decimal", field.colName));
                                break;
                            default:
                                break;
                        }
                        sb.AppendLine("        {");
                        sb.AppendLine("            get; set;");
                        sb.AppendLine("        }");
                    }

                    sb.AppendLine("    }");
                    sb.AppendLine("}");
                    sw.Write(sb.ToString());
                    sb.Clear();
                    textBox1.AppendText("[" + tab.Key + "]已生成完毕.........\n");
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string[] tbnames = GetTableNames();
            var ret = GetTableInfo(tbnames);
            CreateDTO(ret);
        }
    }

    internal class FiledInfo
    {
        public string colName { get; set; }
        public string dbType { get; set; }
        public string precision { get; set; }
        public string scale { get; set; }
        public string dbLength { get; set; }
        public string colNote { get; set; }
        public string required { get; set; }
    }
}
