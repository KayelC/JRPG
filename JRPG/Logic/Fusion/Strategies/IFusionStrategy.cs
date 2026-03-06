namespace JRPGPrototype.Logic.Fusion.Strategies
{
	// Interface for a specific fusion logic implementation.
	public interface IFusionStrategy
	{
		// Executes the state mutation logic for a specific fusion type.
		void Execute(FusionContext context);
	}
}