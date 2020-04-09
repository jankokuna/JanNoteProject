using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data;
using System.Data.SqlTypes;
using Microsoft.Practices.EnterpriseLibrary.Data;
using Verygood.Biz.Object;
using System.Reflection;
using System.ComponentModel;
using Verygood.Biz.Logic;
using System.Xml;
using Verygood.Biz;

namespace Verygood.bizbase
{
    public class Biz_Base : IDisposable
    {
        private Database m_db = null;

        public Biz_Base()
        {
            this.m_db = DatabaseFactory.CreateDatabase();
        }

        public Biz_Base(string dataBase)
        {
            this.m_db = DatabaseFactory.CreateDatabase(dataBase);
        }

        public Database Db
        {
            get { return m_db; }
        }

        protected List<string> GetProcedureColumn(DbConnection con, string procedre)
        {
            bool isOpen = false;
            List<string> list = new List<string>();
            if (con == null)
            {
                con = Db.CreateConnection();
                con.Open();
                isOpen = false;
            }
            else
                isOpen = true;

            try
            {
                if (con == null || con.State != ConnectionState.Open) return null;

                DataTable table = con.GetSchema("ProcedureParameters", new string[] { null, null, procedre });
                if (table == null) return null;


                DataColumn name = table.Columns["PARAMETER_NAME"];

                foreach (DataRow row in table.Rows)
                {
                    list.Add(row[name].ToString().Replace("@", "").Replace(":", ""));
                }
            }
            finally
            {
                if (!isOpen) con.Close();
            }

            return list;

        }


        public void AddInParameter(DbCommand cmd, string column, DbType type, object obj)
        {
            Db.AddInParameter(cmd, column, type, obj);
        }

        public void AddOutParameter(DbCommand cmd, string column, DbType type, int size)
        {
            Db.AddOutParameter(cmd, column, type, size);
        }

        public void SetParameter(DbCommand cmd, string column, object obj)
        {
            Db.SetParameterValue(cmd, column, obj);
        }

        public string ConvertBoolToYn(bool val)
        {
            if (val) return "Y";
            else return "N";
        }

        public void AddInParameter(DbCommand cmd, object obj)
        {
            AddInParameter(cmd, obj, "@");
        }

        /// <summary>
        /// DbCommand 에 parameter를 Binding 한다.
        /// </summary>
        /// <param name="cmd">DbCommand</param>
        /// <param name="obj">Binding할 Class</param>
        public void AddInParameter(DbCommand cmd, object obj, string header)
        {
            DbType dbType = DbType.String;
            object value = null;

            List<string> columns = new List<string>();

            if (cmd.CommandType == CommandType.StoredProcedure) columns = GetProcedureColumn(cmd.Connection, cmd.CommandText);

            foreach (PropertyInfo property in obj.GetType().GetProperties())
            {
                DatabaseAttribute[] attribute = (DatabaseAttribute[])property.GetCustomAttributes(typeof(DatabaseAttribute), true);

                if (attribute.Length > 0)
                {
                    // Schema 에서 얻어온 Parameter 값이 없을경우에는 맵핑시키지 않는다.
                    if (columns.Count > 0 && !columns.Contains(attribute[0].Column)) continue;

                    try
                    {
                        value = property.GetValue(obj, null);

                        // SqlDateTime 형식일 경우
                        if (property.PropertyType == typeof(SqlDateTime)) dbType = DbType.DateTime;
                        else if (property.PropertyType == typeof(DateTime)) dbType = DbType.DateTime;
                        //int형일경우
                        else if (property.PropertyType == typeof(int)) dbType = DbType.Int32;
                        //int? 형인경우
                        else if (property.PropertyType == typeof(int?)) dbType = DbType.Int32;
                        // decimal 형인경우
                        else if (property.PropertyType == typeof(decimal)) dbType = DbType.Decimal;
                        // single 형인경우
                        else if (property.PropertyType == typeof(Single)) dbType = DbType.Single;
                        // Bool 형식
                        else if (property.PropertyType == typeof(bool))
                        {
                            dbType = DbType.String;
                            value = ((bool)value) ? attribute[0].TrueString : attribute[0].FalseString;
                        }
                        // Enum 형식
                        else if (property.PropertyType.IsEnum) dbType = DbType.Int32;
                        // 기타 형식
                        else
                        {
                            dbType = DbType.String;
                            if (value == null || string.IsNullOrEmpty(value.ToString())) value = null;
                        }

                        attribute[0].IsAssign = true;

                        if (attribute[0].IsDbTypeSpecified)
                        {
                            dbType = attribute[0].DbType;
                        }

                        if (attribute[0].ParameterType == ParameterTypeEnum.In)
                        {

                            Db.AddInParameter(cmd, header + attribute[0].Column, dbType, value);

                            //// Domain Dictionary 에서 해당 Column Domain값이 존재할경우
                            //// 해당 Column Domain 값으로 Length를 설정해준다.
                            //if (Biz_Domain.GetDomainSize(attribute[0].Column) > 0)
                            //{
                            //    cmd.Parameters[header + attribute[0].Column].Size = Biz_Domain.GetDomainSize(attribute[0].Column);
                            //}
                        }
                        else if (attribute[0].ParameterType == ParameterTypeEnum.Out)
                        {
                            Db.AddOutParameter(cmd, header + attribute[0].Column, dbType, (dbType.ToString().ToLower().IndexOf("int") > -1) ? 0 : attribute[0].ParameterSize);
                        }
                        else if (attribute[0].ParameterType == ParameterTypeEnum.InOut)
                        {
                            Db.AddParameter(cmd, header + attribute[0].Column, dbType, ((dbType.ToString().ToLower().IndexOf("int") > -1) ? 0 : attribute[0].ParameterSize)
                                , ParameterDirection.InputOutput, true, 0, 0, null, DataRowVersion.Default, value);
                        }

                    }
                    catch (Exception ex)
                    {
                        if (attribute[0].IsCatch) throw ex;
                    }
                }
            }
        }

        public static void SetWrapRS(object obj, IDataReader reader)
        {
            foreach (PropertyInfo property in obj.GetType().GetProperties())
            {
                DatabaseAttribute[] attribute = (DatabaseAttribute[])property.GetCustomAttributes(typeof(DatabaseAttribute), true);

                if (attribute.Length > 0)
                {
                    //DataTable dt = reader.GetSchemaTable();
                    DataRow[] dr = reader.GetSchemaTable().Select($" ColumnName = '{attribute[0].Column}' ");
                    //var dr = (from r in reader.GetSchemaTable().AsEnumerable() where r.Field<string>("ColumnName") == attribute[0].Column select r);
                    if (dr.Length == 0) continue;
                    if (property.SetMethod == null) continue;
                    try
                    {
                        // SqlDateTime 형식일 경우
                        if (property.PropertyType == typeof(SqlDateTime)) property.SetValue(obj, GetDateTime(reader[attribute[0].Column]), null);
                        //int형일경우
                        else if (property.PropertyType == typeof(int)) property.SetValue(obj, GetInteger(reader[attribute[0].Column]), null);
                        else if (property.PropertyType == typeof(long)) property.SetValue(obj, GetLong(reader[attribute[0].Column]), null);
                        //int? 형인경우
                        else if (property.PropertyType == typeof(int?)) property.SetValue(obj, GetNullInteger(reader[attribute[0].Column]), null);
                        // decimal 형인경우
                        else if (property.PropertyType == typeof(decimal)) property.SetValue(obj, GetDecimal(reader[attribute[0].Column]), null);
                        // single 형인경우
                        else if (property.PropertyType == typeof(Single)) property.SetValue(obj, GetSingle(reader[attribute[0].Column]), null);
                        // Bool 형식
                        else if (property.PropertyType == typeof(bool)) property.SetValue(obj, "YTM".IndexOf(GetString(reader[attribute[0].Column])) > -1 && !string.IsNullOrEmpty(GetString(reader[attribute[0].Column])), null);
                        // Enum 형식
                        else if (property.PropertyType.IsEnum)
                        {
                            if (string.IsNullOrEmpty(GetString(reader[attribute[0].Column]))) continue;
                            property.SetValue(obj, Enum.Parse(property.PropertyType, GetString(reader[attribute[0].Column]), true), null);
                        }
                        // XmlCDataSection 형식
                        else if (property.PropertyType == typeof(XmlCDataSection))
                        {
                            var dummy = new XmlDocument();
                            property.SetValue(obj, dummy.CreateCDataSection(GetString(reader[attribute[0].Column])), null);
                        }
                        // 기타 형식
                        else property.SetValue(obj, GetString(reader[attribute[0].Column]), null);

                        attribute[0].IsAssign = true;
                    }
                    catch (Exception ex)
                    {
                        if (attribute[0].IsCatch) throw ex;
                    }

                }
            }

            if (obj is BaseRS)
                (obj as BaseRS).ChangeEvent();
        }

        public static List<T> SetWrapRS<T>(DataTable dt)
            where T : new()
        {
            List<T> list = new List<T>();

            foreach (DataRow row in dt.Rows)
            {
                T obj = new T();

                SetWrapRS(obj, row);

                list.Add(obj);
            }

            return list;
        }

        public static List<T> SetWrapRS<T>(DataRow[] drs)
            where T : new()
        {
            List<T> list = new List<T>();

            foreach (DataRow row in drs)
            {
                T obj = new T();

                SetWrapRS(obj, row);

                list.Add(obj);
            }

            return list;
        }

        public static void SetWrapRS(object list, DataTable dt)
        {

            foreach (DataRow row in dt.Rows)
            {
                object obj = System.Activator.CreateInstance(list.GetType().GetGenericArguments()[0]);

                SetWrapRS(obj, row);

                list.GetType().InvokeMember("Add", BindingFlags.DeclaredOnly |
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Instance | BindingFlags.InvokeMethod, null, list, new object[] { obj });
            }
        }

        public static void SetWrapRS(object obj, DataRow row)
        {
            foreach (PropertyInfo property in obj.GetType().GetProperties())
            {
                DatabaseAttribute[] attribute = (DatabaseAttribute[])property.GetCustomAttributes(typeof(DatabaseAttribute), true);

                if (attribute.Length > 0)
                {
                    if (!row.Table.Columns.Contains(attribute[0].Column)) continue;
                    if (property.SetMethod == null) continue;
                    try
                    {
                        // SqlDateTime 형식일 경우
                        if (property.PropertyType == typeof(SqlDateTime)) property.SetValue(obj, GetDateTime(row[attribute[0].Column]), null);
                        //int형일경우
                        else if (property.PropertyType == typeof(int)) property.SetValue(obj, GetInteger(row[attribute[0].Column]), null);
                        //int? 형인경우
                        else if (property.PropertyType == typeof(int?)) property.SetValue(obj, GetNullInteger(row[attribute[0].Column]), null);
                        // decimal 형인경우
                        else if (property.PropertyType == typeof(decimal)) property.SetValue(obj, GetDecimal(row[attribute[0].Column]), null);
                        // single 형인경우
                        else if (property.PropertyType == typeof(Single)) property.SetValue(obj, GetSingle(row[attribute[0].Column]), null);
                        // Bool 형식
                        else if (property.PropertyType == typeof(bool)) property.SetValue(obj, "YTM".IndexOf(GetString(row[attribute[0].Column])) > -1 && !string.IsNullOrEmpty(GetString(row[attribute[0].Column])), null);
                        // Enum 형식
                        else if (property.PropertyType.IsEnum)
                        {
                            if (string.IsNullOrEmpty(GetString(row[attribute[0].Column]))) continue;
                            property.SetValue(obj, Enum.Parse(property.PropertyType, GetString(row[attribute[0].Column]), true), null);
                        }
                        // 기타 형식
                        // XmlCDataSection 형식
                        else if (property.PropertyType == typeof(XmlCDataSection))
                        {
                            var dummy = new XmlDocument();
                            property.SetValue(obj, dummy.CreateCDataSection(GetString(row[attribute[0].Column])), null);
                        }
                        else property.SetValue(obj, GetString(row[attribute[0].Column]), null);

                        attribute[0].IsAssign = true;
                    }
                    catch (Exception ex)
                    {
                        if (attribute[0].IsCatch) throw ex;
                    }

                }
            }

            if (obj is BaseRS)
                (obj as BaseRS).ChangeEvent();
        }


        /// <summary>
        /// 멀티 테이블의 한행에서 데이타를 가져온다.
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="param">각각의 테이블과 맵핑되는 Class Object</param>
		public void GetMultiClass(DbCommand cmd, params object[] param)
        {
            using (DataSet ds = Db.ExecuteDataSet(cmd))
            {
                for (int i = 0; i < param.Length; i++)
                {
                    if (ds.Tables[i].Rows.Count > 0)
                    {
                        //if (param[i].GetType().IsAssignableFrom(typeof(List<>)))
                        if (param[i].GetType().GetMethod("Add") != null)
                            SetWrapRS(param[i], ds.Tables[i]);
                        else
                            SetWrapRS(param[i], ds.Tables[i].Rows[0]);
                    }
                }
            }
        }

        /// <summary>
        /// 단일 테이블의 한행에서 데이타를 가져온다.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public T GetDataClass<T>(DbCommand cmd) where T : new()
        {
            using (IDataReader reader = Db.ExecuteReader(cmd))
            {
                T obj = default(T);
                if (reader.Read())
                {
                    obj = new T();
                    SetWrapRS(obj, reader);
                }

                return obj;
            }

        }


        public List<string> GetListString(DbCommand cmd)
        {
            using (IDataReader reader = Db.ExecuteReader(cmd))
            {
                List<string> list = new List<string>();

                while (reader.Read())
                {
                    string obj = reader[0]?.ToString();
                    string obj1 = reader[1]?.ToString();
                    list.Add(obj);
                    list.Add(obj1);
                }

                return list;
            }

        }

        public List<T> GetListClass<T>(DbCommand cmd) where T : new()
        {
            using (IDataReader reader = Db.ExecuteReader(cmd))
            {
                List<T> list = new List<T>();

                while (reader.Read())
                {
                    T obj = new T();
                    SetWrapRS(obj, reader);
                    list.Add(obj);
                }

                return list;
            }

        }

        /// <summary>
        /// 프로퍼티로 컬럼명을 가져온다.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="prop"></param>
        /// <returns></returns>
        public string GetDataCulumn<T>(string prop) where T : new()
        {
            T obj = default(T);
            obj = new T();


            foreach (PropertyInfo property in obj.GetType().GetProperties())
            {
                if (property.Name.Equals(prop))
                {
                    DatabaseAttribute[] attribute = (DatabaseAttribute[])property.GetCustomAttributes(typeof(DatabaseAttribute), true);
                    return attribute[0].Column;
                }
            }

            return string.Empty;
        }


        /// <summary>
        /// String Object 를 판별하여 Null 일 경우는 "" 을 아닐경우는 Object 의 String값을 넘겨준다.
        /// </summary>
        /// <param name="value">String Object</param>
        /// <returns></returns>
        public static string GetString(object value)
        {
            return (value == null) ? string.Empty : value.ToString();
        }

        /// <summary>
        /// Object 가 Null일 경우는 0을 아닐경우는 Object 의 int 값이 넘어간다.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int GetInteger(object value)
        {
            return GetInteger(value, 0);
        }

        // <summary>
        /// Object 가 Null일 경우는 0을 아닐경우는 Object 의 int 값이 넘어간다.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static int? GetNullInteger(object value)
        {
            return (value == System.DBNull.Value || value == null) ? null : (int?)value;
        }

        /// <summary>
        /// Object 가 Null일 경우는 defVaule를 넘겨준다.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="defValue"></param>
        /// <returns></returns>
        public static int GetInteger(object value, int defValue)
        {
            return (value == System.DBNull.Value || value == null) ? defValue : Convert.ToInt32(value);
            //return (value == System.DBNull.Value || value == null) ? defValue : Convert.ToInt64(value);
        }

        /// <summary>
        /// Object 가 Null 일 경우 DateTime.MinValue 값을 돌려준다.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static SqlDateTime GetDateTime(object value)
        {
            //  return (value == System.DBNull.Value) ? SqlDateTime.Null : (SqlDateTime)value;
            return (value == System.DBNull.Value || value == null) ? SqlDateTime.Null : (DateTime)value;
        }

        public static decimal GetDecimal(object value)
        {
            return (value == System.DBNull.Value || value == null) ? 0 : Convert.ToDecimal(value);
        }

        public static Single GetSingle(object value)
        {
            return (value == System.DBNull.Value || value == null) ? 0 : Convert.ToSingle(value);
        }

        public static double GetDouble(object value)
        {
            return (value == System.DBNull.Value || value == null) ? 0 : Convert.ToDouble(value);
        }

        public static long GetLong(object value)
        {
            return (value == System.DBNull.Value || value == null) ? 0 : Convert.ToInt64(value);

        }
        public static string ToBoolean(bool value, string trueStr, string falseStr)
        {
            return (value) ? trueStr : falseStr;
        }

        /// <summary>
        /// Object 가 "Y" 혹은 "T"일 경우 True을 돌려준다.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool GetBoolean(object value)
        {
            string tmp = string.Empty;

            try
            {
                tmp = value.ToString().ToUpper();
            }
            catch { }

            return tmp.Equals("Y") || tmp.Equals("T");
        }

        /// <summary>
        /// 주어진 클래스를 XML 문자열로 변환한다.
        /// </summary>
        /// <param name="obj">XML로 변환할 Object</param>
        /// <returns>XML</returns>
        static public string GetXML(object obj)
        {
            System.Xml.Serialization.XmlSerializer xml;
            System.IO.StringWriter text = new System.IO.StringWriter();

            try
            {
                xml = new System.Xml.Serialization.XmlSerializer(obj.GetType());
                xml.Serialize(text, obj);
                return text.ToString();
            }
            catch
            {
                throw;
            }
            finally
            {
                text.Dispose();
            }
        }

        /// <summary>
        /// source 의 Property의 내용을 target으로 복사한다.
        /// Array 계열의 경우는 복사가 되지 않는다. 
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        public static void Copy(object source, object target)
        {
            foreach (PropertyInfo property in target.GetType().GetProperties())
            {
                try
                {
                    PropertyInfo member = source.GetType().GetProperty(property.Name);
                    //FieldInfo member = source.GetType().GetField(property.Name);
                    if (member != null)
                    {

                        property.SetValue(target, member.GetValue(source, null), null);
                    }
                }
                catch
                {
                    continue;
                }

            }
        }


        #region IDisposable 멤버

        private IntPtr handle;
        private Component component = new Component();
        private bool disposed = false;

        public Biz_Base(IntPtr handle)
        {
            this.handle = handle;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    component.Dispose();
                }

                CloseHandle(handle);
                handle = IntPtr.Zero;

                disposed = true;
            }
        }

        [System.Runtime.InteropServices.DllImport("Kernel32")]
        private extern static Boolean CloseHandle(IntPtr handle);

        ~Biz_Base()
        {
            Dispose(false);
        }

        #endregion
    }
}
