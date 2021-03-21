namespace CK.Observable.League.Tests.MicroMachine
{
    public class MachineCommand
    {
        public MachineCommand( string bugOrNot )
        {
            BugOrNot = bugOrNot;
        }

        public string BugOrNot { get; }
    }
}
