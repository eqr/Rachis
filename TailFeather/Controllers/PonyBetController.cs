namespace TailFeather.Controllers
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;

    using Rachis.Utils;

    using TailFeather.Storage;
    using TailFeather.Storage.PonyBets;

    public class PonyBetController: TailFeatherController
    {
        [HttpGet]
        [Route("tailfeather/pony-bet/betOnPony")]
        public async Task<HttpResponseMessage> BetOnPony(
            [FromUri] string odd,
            [FromUri] string user,
            [FromUri] int amountOfMoney)
        {
            var taskCompletionSource = new TaskCompletionSource<object>();
            try
            {
                var betOnPonyCommand = new BetOnPonyCommand
                                           {
                                               AmountOfMoney = amountOfMoney,
                                               UserId = user,
                                               OddId = odd,
                                               Completion = taskCompletionSource
                                           };

                this.RaftEngine.AppendCommand(betOnPonyCommand);
                await taskCompletionSource.Task;
            }
            catch (NotLeadingException e)
            {
                return RedirectToLeader(e.CurrentLeader, this.Request.RequestUri);
            }

            return this.Request.CreateResponse(HttpStatusCode.Accepted);
        }

        [HttpGet]
        [Route("tailfeather/pony-bet/getBets")]
        public async Task<HttpResponseMessage> GetAllBets()
        {
            var sm = (PonyBetsStateMachine)this.StateMachine;
            return this.Request.CreateResponse(HttpStatusCode.Accepted, sm.Bets);
        }

    }
}