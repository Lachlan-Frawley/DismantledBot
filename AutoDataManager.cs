using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Oracle.ManagedDataAccess.Client;

namespace DismantledBot
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class AutoDBField : Attribute
    {
        public OracleDbType FieldType;
        public int FieldSize;
        public string FieldName = null;

        public AutoDBField(OracleDbType type, int size)
        {
            FieldType = type;
            FieldSize = size;
        }
        public AutoDBField(string name, OracleDbType type, int size)
        {
            FieldName = name;
            FieldType = type;
            FieldSize = size;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public sealed class AutoDBTable : Attribute
    {
        public string TableName;

        public AutoDBTable(string TableName)
        {
            this.TableName = TableName;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class AutoDBNoWrite : Attribute
    {

    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class AutoDBIsPrimaryKey : Attribute
    {

    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public sealed class AutoDBIsForiegnKey : Attribute
    {
        public List<AutoDBFieldInformation> PrimaryKeys { get; private set; }

        public AutoDBIsForiegnKey(Type ForeignTableType, params string[] foriegnTableKeyFields)
        {
            var allFields = Utilities.GetAutoDBFieldInformation(ForeignTableType);
            var keyFiltered = allFields.Where(x => x.IsPrimaryKey);
            if(foriegnTableKeyFields == null && keyFiltered.Count() == 1)
            {
                PrimaryKeys = new List<AutoDBFieldInformation>();
                PrimaryKeys.Add(keyFiltered.First());
            } else
            {
                var actualKeys = keyFiltered.Where(x => foriegnTableKeyFields.Contains(x.FieldData.FieldName));
                if (actualKeys.Count() != foriegnTableKeyFields.Length)
                {
                    Console.WriteLine("Primary Keys & Selected Primary Keys not equal...");
                    Environment.Exit(-1);
                }
                PrimaryKeys = actualKeys.ToList();
            }            
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public sealed class AutoDBMultiKey : Attribute
    {
        public OracleDbType FieldType;
        public string FieldName;

        public AutoDBMultiKey(OracleDbType type, string name)
        {
            FieldType = type;
            FieldName = name;
        }
    }

    public sealed class AutoDBFieldInformation
    {
        public AutoDBField FieldData { get; private set; }
        public bool IsNoWrite { get; private set; }
        public bool IsPrimaryKey { get; private set; }    
        public bool IsContainingFieldPrimitive { get; private set; }
        public AutoDBTable ForiegnKeyData { get; private set; }
        public List<AutoDBMultiKey> ForiegnKeyMappings { get; private set; }
        public bool IsForiegnKey { get => ForiegnKeyData != null; }

        private FieldInfo _field = null;
        private PropertyInfo _property = null;

        public AutoDBFieldInformation(FieldInfo field)
        {
            if (field == null)
                throw new Exception();
            _field = field;
            FieldData = field.GetAutoField();
            if (FieldData.FieldName == null)
                FieldData.FieldName = field.Name;
            IsNoWrite = field.GetCustomAttribute<AutoDBNoWrite>() != null;
            IsPrimaryKey = field.GetCustomAttribute<AutoDBIsPrimaryKey>() != null;
            ForiegnKeyData = field.GetType().GetAutoTable();
            IsContainingFieldPrimitive = field.FieldType.IsPrimitive;
            if (IsForiegnKey)
                ForiegnKeyMappings = field.GetCustomAttributes<AutoDBMultiKey>().ToList();
        }

        public AutoDBFieldInformation(PropertyInfo property)
        {
            if (property == null)
                throw new Exception();
            _property = property;
            FieldData = property.GetAutoField();
            if (FieldData.FieldName == null)
                FieldData.FieldName = property.Name;
            IsNoWrite = property.GetCustomAttribute<AutoDBNoWrite>() != null;
            IsPrimaryKey = property.GetCustomAttribute<AutoDBIsPrimaryKey>() != null;
            ForiegnKeyData = property.GetType().GetAutoTable();
            IsContainingFieldPrimitive = property.PropertyType.IsPrimitive;
            if (IsForiegnKey)
                ForiegnKeyMappings = property.GetCustomAttributes<AutoDBMultiKey>().ToList();
        }

        public object GetValue(object obj)
        {
            return _field == null ? _property.GetValue(obj) : _field.GetValue(obj);
        }

        public void SetValue(object obj, object value)
        {
            if (_field == null)
                _property.SetValue(obj, value);
            else
                _field.SetValue(obj, value);
        }
    }
}
