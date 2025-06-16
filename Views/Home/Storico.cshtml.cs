using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using online.Models;
using Online.Data;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Online.Pages
{
    public class StoricoModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public StoricoModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IList<MacchinaOpcUaLog> Logs { get; set; }
        public List<string> Macchine { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SelectedMacchina { get; set; }

        [BindProperty(SupportsGet = true)]
        public string SearchString { get; set; }

        public async Task OnGetAsync()
        {
            // 1. Prepara la lista di macchine per il dropdown
            Macchine = await _context.MacchineOpcUaLog
                                     .Select(m => m.NomeMacchina)
                                     .Distinct()
                                     .ToListAsync();

            // 2. Esegue la query per recuperare i log filtrati
            var logsQuery = _context.MacchineOpcUaLog.AsQueryable();

            if (!string.IsNullOrEmpty(SelectedMacchina))
            {
                logsQuery = logsQuery.Where(l => l.NomeMacchina == SelectedMacchina);
            }

            if (!string.IsNullOrEmpty(SearchString))
            {
                logsQuery = logsQuery.Where(l => l.Nome.Contains(SearchString));
            }

            Logs = await logsQuery.OrderByDescending(l => l.Timestamp).ToListAsync();
        }
    }
}
