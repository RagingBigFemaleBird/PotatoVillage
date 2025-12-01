using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    public class PingMin : Role
    {
        public static Dictionary<string, object> roleDict;

        public PingMin()
        {
            roleDict = new Dictionary<string, object>()
            {
                { YuYanJia.dictYuYanJiaResult, 1 },
            };
        }

        public Dictionary<string, object> RoleDict
        {
            get
            {
                return roleDict;
            }
        }
        public string Name
        {
            get
            {
                return "PingMin";
            }
        }

        public int Version
        {
            get
            {
                return 1;
            }
        }

        public int ActionOrder
        {
            get
            {
                return -1;
            }
        }

        public int ActionDuration
        {
            get
            {
                return -1;
            }
        }

        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            return GameActionResult.NotExecuted;
        }

    }
}
