namespace TailFeather.Storage.PonyBets
{
    using Rachis.Commands;

    public class BetOnPonyCommand:Command
    {
        public string UserId { get; set; }
        public string OddId { get; set; }
        public int AmountOfMoney { get; set; }
    }
}