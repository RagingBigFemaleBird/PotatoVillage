using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.Core
{
    public enum GameActionResult
    {
        NotExecuted,
        Continue, // will continue to process from the current one
        Restart,  // will restart from beginning
    }
    public interface GameAction
    {
        int Version { get; }
        GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update);
    }
}
