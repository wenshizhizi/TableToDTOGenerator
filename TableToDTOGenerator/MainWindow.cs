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
    public partial class MainWindow : Form
    {
        #region 获取基本信息
        private static readonly string infoSql = @"select * from (select 
                                                        [tbname]=c.Name,
                                                        [tabDescribe]=isnull(f.[value],''),
                                                        [colName]=a.Name,
                                                        [dbType]=b.Name,
                                                        [byteLength]=case when a.[max_length]=-1 and b.Name!='xml' then 2147483647
                                                                when b.Name='xml' then 2147483647
                                                                else rtrim(a.[max_length]) end,
                                                        [dbLength]=ColumnProperty(a.object_id,a.Name,'Precision'),
                                                        [decimalDigits]=isnull(ColumnProperty(a.object_id,a.Name,'Scale'),0),
                                                        [required]=case when a.is_nullable=1 then 'true' else 'false' end,
                                                        [colDescribe]=isnull(e.[value],''),
                                                        [devalue]=isnull(d.text,'')    
                                                    from 
                                                        sys.columns a 
                                                    left join
                                                        sys.types b on a.user_type_id=b.user_type_id
                                                    inner join
                                                        sys.objects c on a.object_id=c.object_id and c.Type='U'
                                                    left join
                                                        syscomments d on a.default_object_id=d.ID
                                                    left join
                                                        sys.extended_properties e on e.major_id=c.object_id and e.minor_id=a.Column_id and e.class=1 
                                                    left join
                                                        sys.extended_properties f on f.major_id=c.object_id and f.minor_id=0 and f.class=1) info
                                                    where info.[tbname] = '{0}'";

        private static readonly string VIEWS_LIST_SQL = @"select v.name from sys.views as v,sys.schemas as s where v.schema_id = s.schema_id and s.[name] = 'dbo'";

        private static readonly string VIEWS_INFO_SQL = @"select distinct * from (select 
                                                                [tbname]=c.Name,    
                                                                [colName]=a.Name,
                                                                [dbType]=b.Name    
                                                            from 
                                                                sys.columns a 
                                                            left join
                                                                sys.types b on a.user_type_id=b.user_type_id
                                                            inner join
                                                                sys.objects c on a.object_id=c.object_id and c.Type='V'
                                                            left join
                                                                syscomments d on a.default_object_id=d.ID
                                                            left join
                                                                sys.extended_properties e on e.major_id=c.object_id and e.minor_id=a.Column_id and e.class=1 
                                                            left join
                                                                sys.extended_properties f on f.major_id=c.object_id and f.minor_id=0 and f.class=1) info
                                                            where info.[tbname] = '{0}'";

        public MainWindow()
        {
            InitializeComponent();
        }

        private string[] GetTableNames(int type)
        {
            SqlConnection conn = null;
            try
            {
                conn = new SqlConnection(textBox2.Text);
                conn.Open();

                if (type == 0)
                {
                    string[] restrictions = new string[4];
                    List<string> tabNames = new List<string>();
                    restrictions[0] = textBox3.Text;
                    DataTable table = conn.GetSchema("Tables");

                    conn.Close();

                    foreach (System.Data.DataRow row in table.Rows)
                    {
                        if (row.ItemArray[2].ToString().StartsWith("T"))
                            tabNames.Add(row.ItemArray[2].ToString());
                    }

                    return tabNames.ToArray<string>();
                }
                else
                {
                    List<string> tabNames = new List<string>();
                    using (SqlCommand command = new SqlCommand(VIEWS_LIST_SQL, conn))
                    {
                        var reader = command.ExecuteReader(CommandBehavior.CloseConnection);
                        while (reader.Read())
                        {
                            tabNames.Add(reader["name"].ToString());
                        }
                        reader.Close();
                    }
                    return tabNames.ToArray();
                }
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
                                fi.colName = reader["colName"].ToString();
                                fi.dbType = reader["dbType"].ToString();
                                fi.colDescribe = reader["colDescribe"].ToString();
                                fi.dbLength = reader["dbLength"].ToString();
                                fi.decimalDigits = reader["decimalDigits"].ToString();
                                fi.devalue = reader["devalue"].ToString();
                                fi.required = reader["required"].ToString();
                                fi.byteLength = reader["byteLength"].ToString();
                                fi.tabDescribe = reader["tabDescribe"].ToString();
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

        private Dictionary<string, List<FiledInfo>> GetViewInfo(string[] viewNames)
        {
            Dictionary<string, List<FiledInfo>> ret = new Dictionary<string, List<FiledInfo>>();
            SqlConnection conn = null;
            try
            {
                conn = new SqlConnection(textBox2.Text);
                conn.Open();

                foreach (var item in viewNames)
                {
                    string sql = string.Format(VIEWS_INFO_SQL, item);
                    using (SqlCommand cmd = new SqlCommand(sql, conn))
                    {
                        List<FiledInfo> filed = new List<FiledInfo>();
                        using (var reader = cmd.ExecuteReader(CommandBehavior.Default))
                        {
                            while (reader.Read())
                            {
                                FiledInfo fi = new FiledInfo();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    fi.colName = reader["colName"].ToString();
                                    fi.dbType = reader["dbType"].ToString();
                                }
                                filed.Add(fi);
                            }
                        }
                        ret.Add(item, filed);
                    }
                }
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

        private void CreateView(Dictionary<string, List<FiledInfo>> vinfo)
        {
            var nameSpace = textBox5.Text;
            var authorName = textBox4.Text;
            string dir = System.IO.Path.Combine(System.Environment.CurrentDirectory, "View");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            StringBuilder sb = new StringBuilder();
            foreach (var tab in vinfo)
            {
                string fileName = Path.Combine(dir, tab.Key + "View.cs");
                using (StreamWriter sw = File.CreateText(fileName))
                {
                    sb.AppendLine("/*");
                    sb.AppendLine(string.Format("* 命名空间: {0}", nameSpace));
                    sb.AppendLine("*");
                    sb.AppendLine(string.Format(string.Format("* 功 能： {0}视图实体类", tab.Key)));
                    sb.AppendLine("*");
                    sb.AppendLine(string.Format("* 类 名： {0}View", tab.Key));
                    sb.AppendLine("*");
                    sb.AppendLine("* Version   变更日期            负责人     变更内容");
                    sb.AppendLine("* ─────────────────────────────────────────────────");
                    sb.AppendLine(string.Format("* V1.0.1    {0} {1}     创建", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), authorName));
                    sb.AppendLine("*");
                    sb.AppendLine("* Copyright (c) 2016 Lir Corporation. All rights reserved.");
                    sb.AppendLine("*/");

                    sb.AppendLine("");
                    sb.AppendLine(string.Format("namespace {0}", nameSpace));
                    sb.AppendLine("{");

                    sb.AppendLine("    using System;");
                    sb.AppendLine("");
                    sb.AppendLine("    /// <summary>");
                    sb.AppendLine(string.Format("    /// {0}", tab.Value[0].tabDescribe));
                    sb.AppendLine("    /// </summary>");
                    sb.AppendLine("    [Serializable]");
                    sb.AppendLine(string.Format("    [TableInfo(TableName = \"{0}\")]", tab.Key));
                    sb.AppendLine(string.Format("    public class {0}Entity", tab.Key));
                    sb.AppendLine("    {");

                    foreach (var field in tab.Value)
                    {
                        sb.AppendLine("");
                        sb.AppendLine("        /// <summary>");
                        sb.AppendLine(string.Format("        /// {0}", field.colDescribe));
                        sb.AppendLine("        /// </summary>");

                        switch (field.dbType)
                        {
                            case "uniqueidentifier":
                                sb.AppendLine(string.Format("        [FieldInfo(ColumnName = \"{0}\")]", field.colName));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Guid", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "xml":
                                sb.AppendLine(string.Format("        [FieldInfo(ColumnName = \"{0}\")]", field.colName));
                                sb.AppendLine(string.Format("        public {0} {1}", "String", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "nvarchar":
                                sb.AppendLine(string.Format("        [FieldInfo(ColumnName = \"{0}\")]", field.colName));
                                sb.AppendLine(string.Format("        public {0} {1}", "String", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "varchar":
                                sb.AppendLine(string.Format("        [FieldInfo(ColumnName = \"{0}\")]", field.colName));
                                sb.AppendLine(string.Format("        public {0} {1}", "String", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "int":
                                sb.AppendLine(string.Format("        [FieldInfo(ColumnName = \"{0}\")]", field.colName));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Int32", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "bigint":
                                sb.AppendLine(string.Format("        [FieldInfo(ColumnName = \"{0}\")]", field.colName));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Int64", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "datetime":
                                sb.AppendLine(string.Format("        [FieldInfo(ColumnName = \"{0}\")]", field.colName));
                                sb.AppendLine(string.Format("        public {0}? {1}", "DateTime", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "bit":
                                sb.AppendLine(string.Format("        [FieldInfo(ColumnName = \"{0}\")]", field.colName));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Boolean", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "float":
                                sb.AppendLine(string.Format("        [FieldInfo(ColumnName = \"{0}\")]", field.colName));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Double", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "tinyint":
                                sb.AppendLine(string.Format("        [FieldInfo(ColumnName = \"{0}\")]", field.colName));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Byte", field.colName.ToString().Remove(0, 2)));
                                break;
                            case "smallint":
                                sb.AppendLine(string.Format("        [FieldInfo(ColumnName = \"{0}\")]", field.colName));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Int16", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "real":
                                sb.AppendLine(string.Format("        [FieldInfo(ColumnName = \"{0}\")]", field.colName));
                                sb.AppendLine(string.Format("        public {0} {1}", "float", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "money":
                                sb.AppendLine(string.Format("        [FieldInfo(ColumnName = \"{0}\")]", field.colName));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Decimal", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "decimal":
                                sb.AppendLine(string.Format("        [FieldInfo(ColumnName = \"{0}\")]", field.colName));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Decimal", field.colName.ToString().Remove(0, 2)));
                                break;
                            case "hierarchyid":
                                sb.AppendLine(string.Format("        [FieldInfo(ColumnName = \"{0}\")]", field.colName));
                                sb.AppendLine(string.Format("        public {0} {1}", "String", field.colName.ToString().Remove(0, 1)));
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

        private void CreateDTO(Dictionary<string, List<FiledInfo>> tinfo)
        {
            var nameSpace = textBox3.Text;
            var authorName = textAuthorName.Text;
            string dir = System.IO.Path.Combine(System.Environment.CurrentDirectory, "DTO");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            StringBuilder sb = new StringBuilder();
            foreach (var tab in tinfo)
            {
                string fileName = Path.Combine(dir, tab.Key + "Entity.cs");
                using (StreamWriter sw = File.CreateText(fileName))
                {
                    sb.AppendLine("/*");
                    sb.AppendLine(string.Format("* 命名空间: {0}", nameSpace));
                    sb.AppendLine("*");
                    sb.AppendLine(string.Format(string.Format("* 功 能： {0}实体类", tab.Key)));
                    sb.AppendLine("*");
                    sb.AppendLine(string.Format("* 类 名： {0}Entity", tab.Key));
                    sb.AppendLine("*");
                    sb.AppendLine("* Version   变更日期            负责人     变更内容");
                    sb.AppendLine("* ─────────────────────────────────────────────────");
                    sb.AppendLine(string.Format("* V1.0.1    {0} {1}     创建", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), authorName));
                    sb.AppendLine("*");
                    sb.AppendLine("* Copyright (c) 2016 Lir Corporation. All rights reserved.");
                    sb.AppendLine("*/");

                    sb.AppendLine("");
                    sb.AppendLine(string.Format("namespace {0}", nameSpace));
                    sb.AppendLine("{");

                    sb.AppendLine("    using System;");
                    sb.AppendLine("");
                    sb.AppendLine("    /// <summary>");
                    sb.AppendLine(string.Format("    /// {0}", tab.Value[0].tabDescribe));
                    sb.AppendLine("    /// </summary>");
                    sb.AppendLine("    [Serializable]");
                    sb.AppendLine(string.Format("    [TableInfo(TableName = \"{0}\")]", tab.Key));
                    sb.AppendLine(string.Format("    public class {0}Entity", tab.Key));
                    sb.AppendLine("    {");

                    foreach (var field in tab.Value)
                    {
                        sb.AppendLine("");
                        sb.AppendLine("        /// <summary>");
                        sb.AppendLine(string.Format("        /// {0}", field.colDescribe));
                        sb.AppendLine("        /// </summary>");

                        switch (field.dbType)
                        {
                            case "uniqueidentifier":
                                sb.AppendLine(string.Format("        [FieldInfo(ByteLength = {0},DataLength = {1},DecimalDigits = {2},ColumnName = \"{3}\",Required = {4},DefaultValue = \"{5}\")]", field.byteLength, field.dbLength, field.decimalDigits, field.colName, field.required, 32));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Guid", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "xml":
                                sb.AppendLine(string.Format("        [FieldInfo(ColumnName = \"{0}\")]", field.colName));
                                sb.AppendLine(string.Format("        public {0} {1}", "String", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "nvarchar":
                                sb.AppendLine(string.Format("        [FieldInfo(ByteLength = {0},DataLength = {1},DecimalDigits = {2},ColumnName = \"{3}\",Required = {4},DefaultValue = \"{5}\")]", field.byteLength, field.dbLength, field.decimalDigits, field.colName, field.required, field.devalue));
                                sb.AppendLine(string.Format("        public {0} {1}", "String", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "varchar":
                                sb.AppendLine(string.Format("        [FieldInfo(ByteLength = {0},DataLength = {1},DecimalDigits = {2},ColumnName = \"{3}\",Required = {4},DefaultValue = \"{5}\")]", field.byteLength, field.dbLength, field.decimalDigits, field.colName, field.required, field.devalue));
                                sb.AppendLine(string.Format("        public {0} {1}", "String", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "int":
                                sb.AppendLine(string.Format("        [FieldInfo(ByteLength = {0},DataLength = {1},DecimalDigits = {2},ColumnName = \"{3}\",Required = {4},DefaultValue = \"{5}\")]", field.byteLength, field.dbLength, field.decimalDigits, field.colName, field.required, field.devalue));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Int32", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "bigint":
                                sb.AppendLine(string.Format("        [FieldInfo(ByteLength = {0},DataLength = {1},DecimalDigits = {2},ColumnName = \"{3}\",Required = {4},DefaultValue = \"{5}\")]", field.byteLength, field.dbLength, field.decimalDigits, field.colName, field.required, field.devalue));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Int64", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "datetime":
                                sb.AppendLine(string.Format("        [FieldInfo(ByteLength = {0},DataLength = {1},DecimalDigits = {2},ColumnName = \"{3}\",Required = {4},DefaultValue = \"{5}\")]", field.byteLength, field.dbLength, field.decimalDigits, field.colName, field.required, field.devalue));
                                sb.AppendLine(string.Format("        public {0}? {1}", "DateTime", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "bit":
                                sb.AppendLine(string.Format("        [FieldInfo(ByteLength = {0},DataLength = {1},DecimalDigits = {2},ColumnName = \"{3}\",Required = {4},DefaultValue = \"{5}\")]", field.byteLength, field.dbLength, field.decimalDigits, field.colName, field.required, field.devalue));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Boolean", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "float":
                                sb.AppendLine(string.Format("        [FieldInfo(ByteLength = {0},DataLength = {1},DecimalDigits = {2},ColumnName = \"{3}\",Required = {4},DefaultValue = \"{5}\")]", field.byteLength, field.dbLength, field.decimalDigits, field.colName, field.required, field.devalue));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Double", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "tinyint":
                                sb.AppendLine(string.Format("        [FieldInfo(ByteLength = {0},DataLength = {1},DecimalDigits = {2},ColumnName = \"{3}\",Required = {4},DefaultValue = \"{5}\")]", field.byteLength, field.dbLength, field.decimalDigits, field.colName, field.required, field.devalue));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Byte", field.colName.ToString().Remove(0, 2)));
                                break;
                            case "smallint":
                                sb.AppendLine(string.Format("        [FieldInfo(ByteLength = {0},DataLength = {1},DecimalDigits = {2},ColumnName = \"{3}\",Required = {4},DefaultValue = \"{5}\")]", field.byteLength, field.dbLength, field.decimalDigits, field.colName, field.required, field.devalue));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Int16", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "real":
                                sb.AppendLine(string.Format("        [FieldInfo(ByteLength = {0},DataLength = {1},DecimalDigits = {2},ColumnName = \"{3}\",Required = {4},DefaultValue = \"{5}\")]", field.byteLength, field.dbLength, field.decimalDigits, field.colName, field.required, field.devalue));
                                sb.AppendLine(string.Format("        public {0} {1}", "float", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "money":
                                sb.AppendLine(string.Format("        [FieldInfo(ByteLength = {0},DataLength = {1},DecimalDigits = {2},ColumnName = \"{3}\",Required = {4},DefaultValue = \"{5}\")]", field.byteLength, field.dbLength, field.decimalDigits, field.colName, field.required, field.devalue));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Decimal", field.colName.ToString().Remove(0, 1)));
                                break;
                            case "decimal":
                                sb.AppendLine(string.Format("        [FieldInfo(ByteLength = {0},DataLength = {1},DecimalDigits = {2},ColumnName = \"{3}\",Required = {4},DefaultValue = \"{5}\")]", field.byteLength, field.dbLength, field.decimalDigits, field.colName, field.required, field.devalue));
                                sb.AppendLine(string.Format("        public {0}? {1}", "Decimal", field.colName.ToString().Remove(0, 2)));
                                break;
                            case "hierarchyid":
                                sb.AppendLine(string.Format("        [FieldInfo(ByteLength = {0},DataLength = {1},DecimalDigits = {2},ColumnName = \"{3}\",Required = {4},DefaultValue = \"{5}\")]", field.byteLength, field.dbLength, field.decimalDigits, field.colName, field.required, field.devalue));
                                sb.AppendLine(string.Format("        public {0} {1}", "String", field.colName.ToString().Remove(0, 1)));
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
            if (string.IsNullOrEmpty(textBox3.Text))
            {
                MessageBox.Show("请输入数据实体命名空间");
                return;
            }

            if (string.IsNullOrEmpty(textAuthorName.Text))
            {
                MessageBox.Show("请输入注释作者名");
                return;
            }

            string[] tbnames = GetTableNames(0);
            var ret = GetTableInfo(tbnames);
            if (ret != null)
                CreateDTO(ret);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textBox5.Text))
            {
                MessageBox.Show("请输入视图实体命名空间");
                return;
            }

            if (string.IsNullOrEmpty(textAuthorName.Text))
            {
                MessageBox.Show("请输入注释作者名");
                return;
            }

            string[] tbnames = GetTableNames(1);
            var ret = GetViewInfo(tbnames);
            if (ret != null)
                CreateView(ret);
        }
    }

    internal class FiledInfo
    {
        /// <summary>
        /// 列名
        /// </summary>
        public string colName { get; set; }
        /// <summary>
        /// 表说明
        /// </summary>
        public string tabDescribe { get; set; }
        /// <summary>
        /// 数据类型
        /// </summary>
        public string dbType { get; set; }
        /// <summary>
        /// 字节数
        /// </summary>                  
        public string byteLength { get; set; }
        /// <summary>
        /// 长度
        /// </summary>
        public string dbLength { get; set; }
        /// <summary>
        /// 小数位数
        /// </summary>
        public string decimalDigits { get; set; }
        /// <summary>
        /// 列描述
        /// </summary>
        public string colDescribe { get; set; }
        /// <summary>
        /// 是否必须
        /// </summary>
        public string required { get; set; }
        /// <summary>
        /// 默认值
        /// </summary>
        public string devalue { get; set; }
    }
}
