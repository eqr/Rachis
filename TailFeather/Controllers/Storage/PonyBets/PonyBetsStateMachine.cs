namespace TailFeather.Storage.PonyBets
{
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;

    using Rachis.Commands;
    using Rachis.Interfaces;
    using Rachis.Messages;

    public class PonyBetsStateMachine: IRaftStateMachine
    {
        public PonyBetsStateMachine()
        {
            // TODO maybe wrong, look at the state machine lifetime 
            this.Bets = new List<Bet>();
        }

        public void Dispose()
        {
            
        }

        public long LastAppliedIndex { get; set; }

        public void Apply(LogEntry entry, Command cmd)
        {
            var betCmd = cmd as BetOnPonyCommand;
            if (betCmd != null)
            {
                var odd = OddsReference.AvailableOdds.FirstOrDefault(o => o.OddId == betCmd.OddId);
                if (odd != null)
                {
                    this.Bets.Add(new Bet() { Odd = odd, UserId = betCmd.UserId, AmountOfMoney = betCmd.AmountOfMoney });
                    cmd.CommandResult = new { Result = "Success" };
                }
                else
                {
                    // TODO: figure out what to do if the odd is invalid
                }
            }

            var getCmd = cmd as GetAllBetsCommand;
            if (getCmd != null)
            {
                cmd.CommandResult = this.Bets;
            }

            this.LastAppliedIndex = cmd.AssignedIndex;
        }

        public List<Bet> Bets { get; set; }

        public bool SupportSnapshots
        {
            get
            {
                return false;
            }
        }

        public void CreateSnapshot(long index, long term, ManualResetEventSlim allowFurtherModifications)
        {
            throw new System.NotImplementedException();
        }

        public ISnapshotWriter GetSnapshotWriter()
        {
            throw new System.NotImplementedException();
        }

        public void ApplySnapshot(long term, long index, Stream stream)
        {
            throw new System.NotImplementedException();
        }
    }
}