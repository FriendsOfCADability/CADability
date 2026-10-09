global using Microsoft.VisualStudio.TestTools.UnitTesting;

// enable parallel execution of test methods in this assembly
// 0 means that the test framework will determine the number of workers based on the available processors
// scope of ExecutionScope.MethodLevel means that each test method can be executed in parallel with other test methods
// to exclude a specific test method from parallel execution, you can use the [DoNotParallelize] attribute on that method
[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
