namespace Iterate.Domain.Content
{
    /// <summary>
    /// The closed, versioned v0.1 operation primitive set (TA-DAT-003 as revised). Each member names
    /// one sealed <see cref="EffectOperation"/> derived record. Serialized in JSON as the uppercase
    /// operation tokens (QUANTITY_CHANGE, …, TARGET_LOCK_UPDATE).
    /// </summary>
    public enum OperationKind
    {
        QuantityChange,
        DispositionChange,
        AddedExecutionRequest,
        CounterRequest,
        CostModification,
        Rescue,
        PredictionVisibility,
        ConfigurationModification,
        OperationModification,
        TargetLockUpdate
    }
}