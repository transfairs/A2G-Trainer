using Xunit;

// AddressPresets/Settings hold mutable static state that several tests exercise
// (InitOpponent/InitDynamicTeam, the token cache in Tools). Running collections in
// parallel would make those tests race each other non-deterministically.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
