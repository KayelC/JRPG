using JRPGPrototype.Core;
using JRPGPrototype.Logic.Fusion.Strategies;
using System.Collections.Generic;

namespace JRPGPrototype.Logic.Fusion
{
    public class FusionStrategyRegistry
    {
        private readonly Dictionary<FusionOperationType, IFusionStrategy> _strategies = new();

        public FusionStrategyRegistry()
        {
            _strategies[FusionOperationType.CreateNewDemon] = new StandardFusionStrategy();
            _strategies[FusionOperationType.RankUpParent] = new RankMutationStrategy();
            _strategies[FusionOperationType.RankDownParent] = new RankMutationStrategy();
            _strategies[FusionOperationType.StatBoostFusion] = new StatBoostStrategy();
        }

        public IFusionStrategy? GetStrategy(FusionOperationType type)
        {
            return _strategies.GetValueOrDefault(type);
        }
    }
}