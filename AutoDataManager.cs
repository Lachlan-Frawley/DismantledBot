using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Oracle.ManagedDataAccess.Client;

namespace DismantledBot
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class AutoDBField : Attribute
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
    public class AutoDBTable : Attribute
    {
        public string TableName;

        public AutoDBTable(string TableName)
        {
            this.TableName = TableName;
        }
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
    public class AutoDBIgnore : Attribute
    {

    }

    public abstract class AutoDBBase<T> : IEqualityComparer<T> where T : AutoDBBase<T>
    {
        public abstract bool Equals([AllowNull] T x, [AllowNull] T y);

        public abstract int GetHashCode([DisallowNull] T obj);
    }
}
