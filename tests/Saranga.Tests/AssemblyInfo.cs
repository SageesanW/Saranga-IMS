using Xunit;

// Disable parallel test execution across classes since integration tests run against a shared live database
[assembly: CollectionBehavior(DisableTestParallelization = true)]
