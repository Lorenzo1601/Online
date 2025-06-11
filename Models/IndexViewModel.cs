using System.Collections.Generic;

namespace Online.Models
{
    public class IndexViewModel
    {
        public IEnumerable<Macchina> Macchine { get; set; }
        public MacchinaInputModel NuovaMacchina { get; set; }
    }
}
