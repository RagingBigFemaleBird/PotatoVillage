using ProcedureCore.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace ProcedureCore.LangRenSha
{
    public class YuYanJia : Role
    {
        public static Dictionary<string, object> roleDict;

        public YuYanJia()
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
                return "YuYanJia";
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
                return 150;
            }
        }

        public int ActionDuration
        {
            get
            {
                return 30;
            }
        }

        public static string dictYuYanJiaResult = "chayan";

        public void ChaYan(Game game, int target, Dictionary<string, object> update)
        {
            update[dictYuYanJiaResult] = ((Dictionary<string, object>)((Dictionary<string, object>)game.StateDictionary[LangRenSha.dictPlayers])[target.ToString()])[dictYuYanJiaResult];
        }

        public GameActionResult GenerateStateDiff(Game game, Dictionary<string, object> update)
        {
            if (game.StateSequenceNumber == 1)
            {

                var players = (Dictionary<string, object>)(game.StateDictionary[LangRenSha.dictPlayers]);
                foreach (var player in players)
                {
                    if (((Dictionary<string, object>)((Dictionary<string, object>)game.StateDictionary[LangRenSha.dictPlayers])[player.Key])[LangRenSha.dictRole].ToString() == Name)
                    {
                        // TODO: version mismatch
                        update[LangRenSha.dictNightOrders] = (List<int>)game.StateDictionary[LangRenSha.dictNightOrders];
                        ((List<int>)update[LangRenSha.dictNightOrders]).Add(ActionOrder);
                        break;
                    }
                }
                return GameActionResult.Continue;
            }

            if ((int)game.StateDictionary[LangRenSha.dictAction] == ActionOrder)
            {
                var yuYanJia = LangRenSha.GetPlayers(game, x => (string)x[LangRenSha.dictRole] == Name);
                var alivePlyaers = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                if (UserAction.EndUserAction(game, update))
                {
                    LangRenSha.AdvanceAction(game, update);
                    return GameActionResult.Restart;
                }
                else
                {
                    if (UserAction.StartUserAction(game, ActionDuration, update))
                    {
                        update[UserAction.dictUserActionTargets] = LangRenSha.GetPlayers(game, x => (int)x[LangRenSha.dictAlive] == 1);
                        update[UserAction.dictUserActionUsers] = yuYanJia;
                        update[UserAction.dictUserActionTargetsCount] = 1;
                        update[UserAction.dictUserActionTargetsHint] = 2;
                        return GameActionResult.Restart;
                    }
                    else
                    {
                        (var inputValid, var input) = UserAction.GetUserResponse(game, true, update);
                        if (inputValid)
                        {
                            var targets = UserAction.TallyUserInput(input, 0, UserAction.UserInputMode.VoteMost);
                            ChaYan(game, targets[0], update);
                            UserAction.EndUserAction(game, update, true);
                            LangRenSha.AdvanceAction(game, update);
                            return GameActionResult.Restart;
                        }
                    }
                }
                return GameActionResult.NotExecuted;
            }
            return GameActionResult.NotExecuted;
        }

    }
}
