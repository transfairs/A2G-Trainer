using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for the application entry point. Main() itself pumps a real WinForms message loop
    /// (Application.Run), so it's driven on a dedicated STA thread and unblocked via an
    /// Application.Idle handler that exits as soon as the loop is actually pumping - exercising
    /// every line of Main() without the test hanging. The two lambda exception handlers wired up
    /// there are invoked directly via reflection (their compiler-generated method group), since
    /// deliberately raising a real unhandled UI-thread/AppDomain exception to trigger them for
    /// real would risk tearing down the test process.
    /// </summary>
    public class ProgramTests
    {
        private static MethodInfo MainMethod => typeof(Program).GetMethod("Main", BindingFlags.NonPublic | BindingFlags.Static);

        [Fact]
        public void Main_RunsAndExitsTheMessageLoopWithoutThrowing()
        {
            Exception thrown = null;
            Thread thread = new Thread(() =>
            {
                EventHandler exitOnIdle = null;
                exitOnIdle = (s, e) =>
                {
                    Application.Idle -= exitOnIdle;
                    Application.Exit();
                };
                Application.Idle += exitOnIdle;

                try
                {
                    MainMethod.Invoke(null, null);
                }
                catch (Exception ex)
                {
                    thrown = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(thrown);
        }

        [Fact]
        public void ThreadExceptionHandler_LogsTheException()
        {
            InvalidOperationException ex = new InvalidOperationException("ProgramTests-thread-exception");

            Exception thrown = Record.Exception(() => InvokeLambda<ThreadExceptionEventArgs>(null, new ThreadExceptionEventArgs(ex)));

            Assert.Null(thrown);
        }

        [Fact]
        public void UnhandledExceptionHandler_LogsTheException()
        {
            InvalidOperationException ex = new InvalidOperationException("ProgramTests-unhandled-exception");

            Exception thrown = Record.Exception(() => InvokeLambda<UnhandledExceptionEventArgs>(null, new UnhandledExceptionEventArgs(ex, false)));

            Assert.Null(thrown);
        }

        // Roslyn compiles Main()'s non-capturing lambdas into instance methods on a compiler-generated
        // nested type (e.g. "<>c"), bound through its cached singleton field (e.g. "<>9"); this locates
        // the one matching the given second-parameter EventArgs type rather than depending on its
        // generated name, and invokes it against that singleton.
        private static void InvokeLambda<TEventArgs>(object sender, TEventArgs args)
        {
            Type generated = typeof(Program).GetNestedTypes(BindingFlags.NonPublic)
                .Single(t => t.Name.StartsWith("<>c", StringComparison.Ordinal));

            MethodInfo method = generated.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Single(m =>
                {
                    ParameterInfo[] parameters = m.GetParameters();
                    return parameters.Length == 2 && parameters[1].ParameterType == typeof(TEventArgs);
                });

            object target = method.IsStatic ? null : generated.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
                .Single(f => f.FieldType == generated).GetValue(null);

            method.Invoke(target, new object[] { sender, args });
        }
    }
}
