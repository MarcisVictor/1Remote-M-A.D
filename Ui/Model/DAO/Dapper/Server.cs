﻿using _1RM.Model.Protocol;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;

namespace _1RM.Model.DAO.Dapper
{
    public class Server : IDataBaseServer
    {
        public int Id { get; set; }
        public string Protocol { get; set; } = "";
        public string ClassVersion { get; set; } = "";
        public string JsonConfigString { get; set; } = "";

        public ProtocolBase? ToProtocolServerBase()
        {
            return ItemCreateHelper.CreateFromDbOrm(this);
        }

        public int GetId()
        {
            return Id;
        }

        public string GetProtocol()
        {
            return Protocol;
        }

        public string GetClassVersion()
        {
            return ClassVersion;
        }

        public string GetJson()
        {
            return JsonConfigString;
        }
    }

    internal static class ServerHelperStatic
    {
        public static Server ToDbServer(this ProtocolBase s)
        {
            var ret = new Server()
            {
                Id = s.Id,
                ClassVersion = s.ClassVersion,
                JsonConfigString = s.ToJsonString(),
                Protocol = s.Protocol,
            };
            return ret;
        }
    }
}