using System.Text.RegularExpressions;
using ValidatorService.Dtos;

namespace ValidatorService.Services
{
    public class GrammarGeneratorService
    {
        private readonly GrammarDto _grammar;
        private readonly Dictionary<string, List<string[]>> _productions;
        private readonly int _maxDepth;

        public GrammarGeneratorService(GrammarDto grammar, int maxDepth = 10)
        {
            _grammar = grammar ?? throw new ArgumentNullException(nameof(grammar));
            _maxDepth = maxDepth;
            _productions = ParseProductions(grammar);
        }

        // Parseo: nonTerminal -> lista de alternativas (cada alternativa = array de tokens)
        private Dictionary<string, List<string[]>> ParseProductions(GrammarDto grammar)
        {
            var dict = new Dictionary<string, List<string[]>>();

            foreach (var p in grammar.Productions)
            {
                var key = p.NonTerminal.Trim();
                if (!dict.ContainsKey(key)) dict[key] = new List<string[]>();

                // separar alternativas por '|'
                var alts = p.RightSide.Split('|')
                    .Select(a => a.Trim())
                    .Where(a => a.Length > 0);

                foreach (var alt in alts)
                {
                    // Si epsilon se representa con 'ε' o con 'epsilon' o con 'ε'
                    if (alt == "ε" || string.Equals(alt, "epsilon", StringComparison.OrdinalIgnoreCase))
                    {
                        dict[key].Add(new string[0]); // alternativa vacía
                        continue;
                    }

                    // separar tokens por espacios (preserva signos como ';' como tokens separados si hay espacio)
                    var tokens = alt.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                    .Select(t => t.Trim())
                                    .ToArray();
                    dict[key].Add(tokens);
                }
            }

            return dict;
        }

        private bool IsNonTerminal(string token)
        {
            return _productions.ContainsKey(token);
        }

        // Genera hasta maxWords palabras (cada palabra es tokens separados por un espacio)
        public List<string> GenerateWords(int maxWords = 1000)
        {
            var results = new HashSet<string>();
            var visited = new HashSet<string>(); // evita encolar estados repetidos

            // estado: lista de tokens (strings), may contain non-terminals
            var queue = new Queue<(string[] tokens, int depth)>();
            queue.Enqueue((new[] { _grammar.StartSymbol }, 0));
            visited.Add(SerializeState(new[] { _grammar.StartSymbol }));

            while (queue.Count > 0 && results.Count < maxWords)
            {
                var (tokens, depth) = queue.Dequeue();

                // si alcanzamos profundidad máxima, no expandimos más
                if (depth > _maxDepth) continue;

                // si no hay no-terminals -> es una palabra final
                if (!tokens.Any(t => IsNonTerminal(t)))
                {
                    // construir palabra: unir tokens con espacio, eliminar posibles símbolos epsilon vacíos
                    var word = string.Join(" ", tokens.Where(t => !string.IsNullOrEmpty(t))).Trim();
                    results.Add(word);
                    continue;
                }

                // Expandir primer no-terminal (leftmost)
                int idx = Array.FindIndex(tokens, t => IsNonTerminal(t));
                if (idx == -1) continue;

                var nonTerm = tokens[idx];
                if (!_productions.TryGetValue(nonTerm, out var alternatives)) continue;

                foreach (var alt in alternatives)
                {
                    // construir nuevo arreglo de tokens: tokens[0..idx-1] + alt + tokens[idx+1..end]
                    var prefix = tokens.Take(idx).ToArray();
                    var suffix = tokens.Skip(idx + 1).ToArray();

                    var newTokens = new List<string>();
                    newTokens.AddRange(prefix);
                    // alt es un string[], puede ser vacío (epsilon)
                    newTokens.AddRange(alt);
                    newTokens.AddRange(suffix);

                    // opcional: limitar tamaño de la cadena para evitar explosión
                    if (newTokens.Count > 30) continue;

                    var serial = SerializeState(newTokens.ToArray());
                    if (visited.Contains(serial)) continue;
                    visited.Add(serial);
                    queue.Enqueue((newTokens.ToArray(), depth + 1));
                }
            }

            return results.ToList();
        }

        private string SerializeState(string[] tokens)
        {
            return string.Join(" ", tokens).Trim();
        }

        // Comprueba pertenencia generando y buscando la palabra (el formato debe coincidir)
        // Ej: para tokens separados por espacio: "int id ;"
        public bool WordBelongs(string word, int maxWords = 1000)
        {
            if (word == null) return false;
            var generated = GenerateWords(maxWords);
            // comparar de forma literal (puede normalizar espacios)
            var normalized = string.Join(" ", word.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            return generated.Contains(normalized);
        }
    }
}
