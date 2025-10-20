using Newtonsoft.Json; // si prefiere System.Text.Json puede cambiar
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ValidatorService.Dtos;
// using System.Text.Json;

namespace ValidatorService.Services
{
    public class GrammarValidatorService
    {
        private readonly HttpClient _httpClient;

        // HttpClient inyectado por DI
        public GrammarValidatorService(HttpClient httpClient)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public string GetMessage()
        {
            return "Hello World";
        }

        public async Task<GrammarDto> GetGrammarByIdAsync(string id)
        {
            var url = $"https://localhost:7107/api/grammar/{id}";

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var grammar = JsonConvert.DeserializeObject<GrammarDto>(responseContent);
                return grammar!;
            }
            catch
            {
                return null;
            }
        }
    }
}
