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
        Continue, // Will continue to process from the current one
        Restart,  // Will restart from beginning
        GameOver, // Game ended, no more actions will be processed
    }
    public interface GameAction
    {
        int Version { get; }
        GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update);
    }
}
