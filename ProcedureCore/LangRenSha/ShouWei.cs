using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    public class ShouWei
    {
        public static Dictionary<string, object> roleDict;
        public ShouWei()
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
                return "ShouWei";
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
                return 50;
            }
        }

        public int ActionDuration
        {
            get
            {
                return 30;
            }
        }

        public static string dictGuardTarget = "shouwei_target";

    }
}
