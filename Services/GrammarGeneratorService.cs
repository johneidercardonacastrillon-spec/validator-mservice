using System.Text;
using System.Text.RegularExpressions;
using ValidatorService.Dtos;

namespace ValidatorService.Services
{
    /// <summary>
    /// Genera palabras válidas a partir de una gramática libre de contexto y 
    /// permite verificar si una palabra pertenece al lenguaje generado.
    /// </summary>
    public class GrammarGeneratorService
    {
        private readonly GrammarDto _grammar;
        private readonly Dictionary<string, List<string[]>> _productions;

        public GrammarGeneratorService(GrammarDto grammar)
        {
            _grammar = grammar ?? throw new ArgumentNullException(nameof(grammar));
            _productions = ParseProductions(grammar);
        }

        // ============================================================
        // == PARSEO DE PRODUCCIONES
        // ============================================================

        /// <summary>
        /// Convierte las producciones de la gramática (en texto) a una estructura interna.
        /// Se crea una estructura clave-valor donde esto:
        /// { "nonTerminal": "Exp", "rightSide": "Exp + Term | Exp - Term | Term" },
        /// pasa a ser esto:
        /// clave: Exp
        /// valor: [["Exp", "+", "Term"], ["Exp", "-", "Term"], ["Term"]]
        /// </summary>
        private Dictionary<string, List<string[]>> ParseProductions(GrammarDto grammar)
        {
            var result = new Dictionary<string, List<string[]>>();

            foreach (var production in grammar.Productions)
            {
                var nonTerminal = production.NonTerminal.Trim();

                if (!result.ContainsKey(nonTerminal))
                    result[nonTerminal] = new List<string[]>();

                var alternatives = SplitAlternatives(production.RightSide);

                foreach (var alt in alternatives)
                    result[nonTerminal].Add(ParseAlternative(alt));
            }

            return result;
        }

        private IEnumerable<string> SplitAlternatives(string rightSide)
        {
            return rightSide.Split('|')
                            .Select(a => a.Trim())
                            .Where(a => a.Length > 0);
        }

        private static string[] ParseAlternative(string alternative)
        {
            if (string.IsNullOrWhiteSpace(alternative))
                return Array.Empty<string>();

            var tokens = Regex.Matches(alternative, @"[A-Za-z_][A-Za-z0-9_]*|[\(\)\{\}\+\-\=\;\|]")
                              .Cast<Match>()
                              .Select(m => m.Value)
                              .ToArray();

            return tokens;
        }

        // ============================================================
        // == GENERACIÓN DE PALABRAS
        // ============================================================

        /// <summary>
        /// Genera palabras válidas a partir de la gramática,
        /// con límites configurables para profundidad, cantidad y tamaño.
        /// </summary>
        public List<string> GenerateWords(int maxDepth = 10, int maxWords = 1000, int maxTokens = 30)
        {
            var results = new HashSet<string>();
            var visitedStates = new HashSet<string>();

            var initial = new[] { _grammar.StartSymbol };
            var queue = new Queue<(string[] tokens, int depth)>();
            queue.Enqueue((initial, 0));
            visitedStates.Add(SerializeTokens(initial));

            while (queue.Count > 0 && results.Count < maxWords)
            {
                var (tokens, depth) = queue.Dequeue();

                if (depth > maxDepth)
                    continue;

                if (IsFinal(tokens))
                {
                    var word = TokensToWord(tokens);
                    results.Add(word);
                    continue;
                }

                Expand(tokens, depth, queue, visitedStates, maxTokens);
            }

            return results.ToList();
        }

        private bool IsFinal(string[] tokens) => !tokens.Any(IsNonTerminal);

        private static string TokensToWord(IEnumerable<string> tokens)
        {
            return string.Join(" ", tokens.Where(t => !string.IsNullOrEmpty(t))).Trim();
        }

        private void Expand(
            string[] tokens,
            int depth,
            Queue<(string[], int)> queue,
            HashSet<string> visited,
            int maxTokens)
        {
            int index = Array.FindIndex(tokens, IsNonTerminal);
            if (index == -1) return;

            var nonTerminal = tokens[index];
            if (!_productions.TryGetValue(nonTerminal, out var alternatives)) return;

            foreach (var alt in alternatives)
            {
                var expandedTokens = BuildExpandedTokens(tokens, index, alt);

                // Evita crecimiento excesivo
                if (expandedTokens.Length > maxTokens) continue;

                var stateKey = SerializeTokens(expandedTokens);
                if (visited.Contains(stateKey)) continue;

                visited.Add(stateKey);
                queue.Enqueue((expandedTokens, depth + 1));
            }
        }

        private static string[] BuildExpandedTokens(string[] tokens, int index, string[] replacement)
        {
            var prefix = tokens.Take(index);
            var suffix = tokens.Skip(index + 1);
            return prefix.Concat(replacement).Concat(suffix).ToArray();
        }

        private static string SerializeTokens(IEnumerable<string> tokens)
        {
            return string.Join(" ", tokens).Trim();
        }

        private bool IsNonTerminal(string token) => _productions.ContainsKey(token);

        // ============================================================
        // == VALIDACIÓN DE PERTENENCIA
        // ============================================================

        public bool WordBelongs(string word, List<string> generated)
        {
            if (string.IsNullOrWhiteSpace(word))
                return false;

            var normalized = NormalizeWord(word);
            return generated.Contains(normalized);
        }

        private static string NormalizeWord(string word)
        {
            return string.Join(" ", word.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
