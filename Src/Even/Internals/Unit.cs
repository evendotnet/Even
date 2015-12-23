using System.Threading.Tasks;

namespace Even.Internals
{
    internal class Unit
    {
        public static readonly Unit Instance = new Unit();
        private Unit()
        {
        }

        public static Task<Unit> GetCompletedTask()
        {
            return Task.FromResult(Instance);
        } 
    }
}