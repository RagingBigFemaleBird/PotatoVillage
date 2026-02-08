using ProcedureCore.Core;
using ProcedureCore.LangRenSha;

namespace CoreTest
{
    public class Tests
    {
        private Game game;
        [SetUp]
        public void Setup()
        {
            game = new Game(ActionCallback);
            game.Actions.Add(new LangRenSha());
            game.Actions.Add(new LangRen());
            game.Actions.Add(new YuYanJia());
        }

        public void ActionCallback(Game game, Dictionary<string, object> stateDiff)
        {
            // do nothing
        }

        [Test]
        public void Test1()
        {
            game.ActionLoop();
            Assert.Pass();
        }
    }
}