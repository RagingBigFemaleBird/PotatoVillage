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
            game = Game.Instance;
            game.Actions.Add(new LangRenSha());
            game.Actions.Add(new LangRen());
            game.Actions.Add(new YuYanJia());
        }

        [Test]
        public void Test1()
        {
            game.ActionLoop();
            Assert.Pass();
        }
    }
}