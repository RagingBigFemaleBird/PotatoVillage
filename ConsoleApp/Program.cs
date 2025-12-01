using ProcedureCore.Core;
using ProcedureCore.LangRenSha;

var game = Game.Instance;

void InputLoop(Game game)
{
    while (true)
    {
        var input = Console.ReadLine();
        if (input == null)
        {
            continue;
        }
        int owner = int.Parse(input);
        Console.WriteLine("Acting as player " + owner);
        (var doInput, var targets, var targets_count) = UserAction.UserActionTargets(game, owner);
        if (doInput)
        {
            string result = string.Join(", ", targets.Select(n => n.ToString()));
            Console.WriteLine("Provide target among " + result);
            var response = Console.ReadLine();
            List<int> ret = new List<int>();
            if (response != null)
            {
                ret.Add(int.Parse(response));
            }
            UserAction.UserActionRespond(game, owner, ret);
        }
    }
}

game.Actions.Add(new LangRenSha());
game.Actions.Add(new LangRen());
game.Actions.Add(new YuYanJia());
Thread input = new Thread(() => InputLoop(game));
input.Start();
game.ActionLoop();