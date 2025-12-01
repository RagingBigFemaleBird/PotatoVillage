using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    public interface Role : GameAction
    {
        string Name { get; }
        int ActionOrder { get; }
        int ActionDuration { get; }
        Dictionary<string, object> RoleDict { get; }
    }
}
