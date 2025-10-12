using Microsoft.VisualStudio.TestTools.UnitTesting;

// TODO: rework SettingsTests execution lock
// If methods from DatabaseTests and SettingsTests are run simultaneously several database tests can fail simultaneously.
// Workaround for now is to limit the number of workers so database tests complete run first. Not guaranteed reliable as
// it is still a race condition, but works well enough in practice.
[assembly: Parallelize(Scope = ExecutionScope.MethodLevel, Workers = 8)]