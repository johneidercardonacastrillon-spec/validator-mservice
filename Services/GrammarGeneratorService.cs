using System.Text;
using System.Text.RegularExpressions;
using ValidatorService.Dtos;

namespace ValidatorService.Services
{
    /// <summary>
    /// Genera palabras válidas a partir de una gramática libre de contexto (CFG) y
    /// permite verificar si una palabra pertenece al lenguaje generado.
    /// Utiliza un enfoque de búsqueda en amplitud (BFS) limitado para la generación.
    /// </summary>
    public class GrammarGeneratorService
    {
        private readonly GrammarDto _grammar;

        /// <summary>
        /// Almacena las producciones en un formato fácil de usar.
        /// Clave: Símbolo No Terminal (string)
        /// Valor: Lista de alternativas, donde cada alternativa es un array de símbolos (string[]).
        /// </summary>
        private readonly Dictionary<string, List<string[]>> _productions;

        public GrammarGeneratorService(GrammarDto grammar)
        {
            // Uso de nameof para el manejo de excepciones
            _grammar = grammar ?? throw new ArgumentNullException(nameof(grammar));
            _productions = ParseProductions(grammar);
        }

        // ============================================================
        // == PRODUCTION PARSING (PARSEO DE PRODUCCIONES)
        // ============================================================

        /// <summary>
        /// Convierte las producciones de la gramática (en texto) a una estructura interna.
        /// Ej. de: { "NonTerminal": "Exp", "RightSide": "Exp + Term | Exp - Term | Term" }
        /// a: Key: "Exp", Value: [["Exp", "+", "Term"], ["Exp", "-", "Term"], ["Term"]]
        /// </summary>
        private Dictionary<string, List<string[]>> ParseProductions(GrammarDto grammar)
        {
            var result = new Dictionary<string, List<string[]>>();

            foreach (var production in grammar.Productions)
            {
                // Convención C#: usar 'nonTerminal' en lugar de 'nonTerminal'
                var nonTerminal = production.NonTerminal.Trim();

                if (!result.ContainsKey(nonTerminal))
                    result[nonTerminal] = new List<string[]>();

                var alternatives = SplitAlternatives(production.RightSide);

                foreach (var alternative in alternatives)
                    result[nonTerminal].Add(ParseAlternative(alternative));
            }

            return result;
        }

        /// <summary>
        /// Divide la parte derecha de una producción por el separador '|'.
        /// </summary>
        private static IEnumerable<string> SplitAlternatives(string rightSide)
        {
            // Uso moderno de string.Split y LINQ para mayor claridad
            return rightSide.Split('|', StringSplitOptions.RemoveEmptyEntries)
                            .Select(a => a.Trim())
                            .Where(a => a.Length > 0);
        }

        /// <summary>
        /// Tokeniza una sola alternativa de producción.
        /// El regex captura:
        /// 1. Identificadores (NonTerminales o Terminales como 'Id', 'number').
        /// 2. Símbolos de un solo caracter ('(', ')', '{', '}', '+', '-', '=', ';', '|').
        /// </summary>
        private static string[] ParseAlternative(string alternative)
        {
            if (string.IsNullOrWhiteSpace(alternative))
                return Array.Empty<string>();

            // El regex original está bien, asegura que no se separen tokens.
            var tokens = Regex.Matches(alternative, @"[A-Za-z_][A-Za-z0-9_]*|[\(\)\{\}\+\-\=\;\|]")
                              .Select(m => m.Value)
                              .ToArray();

            return tokens;
        }

        // ============================================================
        // == WORD GENERATION (GENERACIÓN DE PALABRAS)
        // ============================================================

        /// <summary>
        /// Genera palabras válidas a partir de la gramática,
        /// con límites configurables para profundidad, cantidad y tamaño.
        /// Utiliza una búsqueda en amplitud (BFS).
        /// </summary>
        /// <param name="maxDepth">Profundidad máxima de las derivaciones.</param>
        /// <param name="maxWords">Cantidad máxima de palabras a generar.</param>
        /// <param name="maxTokens">Cantidad máxima de símbolos permitidos en una derivación intermedia.</param>
        public List<string> GenerateWords(int maxDepth = 10, int maxWords = 1000, int maxTokens = 30)
        {
            var results = new HashSet<string>();
            var visitedStates = new HashSet<string>();

            // Usamos 'initialTokens' para mayor claridad
            var initialTokens = new[] { _grammar.StartSymbol };

            // Usamos 'queue' como nombre de variable para mayor claridad
            var queue = new Queue<(string[] tokens, int depth)>();

            queue.Enqueue((initialTokens, 0));
            visitedStates.Add(SerializeTokens(initialTokens));

            // Renombramos 'tokens' y 'depth' en el 'while'
            while (queue.Count > 0 && results.Count < maxWords)
            {
                var (currentTokens, currentDepth) = queue.Dequeue();

                if (currentDepth >= maxDepth) // Cambio a '>=' para reflejar el límite
                    continue;

                // Si no quedan No Terminales, es una palabra final (Terminal).
                if (IsFinal(currentTokens))
                {
                    var word = TokensToWord(currentTokens);
                    results.Add(word);
                    continue;
                }

                // Genera nuevas derivaciones y las añade a la cola.
                Expand(currentTokens, currentDepth, queue, visitedStates, maxTokens);
            }

            return results.ToList();
        }

        /// <summary>
        /// Verifica si la lista de tokens contiene solo símbolos terminales.
        /// </summary>
        private bool IsFinal(string[] tokens) => !tokens.Any(IsNonTerminal);

        /// <summary>
        /// Convierte una lista de tokens terminales en una palabra (string).
        /// </summary>
        private static string TokensToWord(IEnumerable<string> tokens)
        {
            // Uso de string.Join con String.Empty como separador si los tokens representan terminales
            // En el código original usa " " lo cual es adecuado para tokens espaciados.
            return string.Join(" ", tokens.Where(t => !string.IsNullOrEmpty(t))).Trim();
        }

        /// <summary>
        /// Expande el primer símbolo no terminal encontrado en la derivación actual.
        /// </summary>
        private void Expand(
            string[] tokens,
            int depth,
            Queue<(string[] tokens, int depth)> queue,
            HashSet<string> visitedStates, // Renombrado a visitedStates para consistencia
            int maxTokens)
        {
            // Usamos FindIndex para obtener el índice del primer No Terminal
            int indexToExpand = Array.FindIndex(tokens, IsNonTerminal);
            if (indexToExpand == -1) return; // Esto no debería suceder si se llamó a IsFinal antes

            var nonTerminalToExpand = tokens[indexToExpand];

            // Uso de 'out var' para una declaración más concisa. Renombrado 'alternatives' por claridad.
            if (!_productions.TryGetValue(nonTerminalToExpand, out var alternatives)) return;

            foreach (var alternativeReplacement in alternatives)
            {
                var newTokens = BuildExpandedTokens(tokens, indexToExpand, alternativeReplacement);

                // Evita crecimiento excesivo en la longitud de la cadena de símbolos
                if (newTokens.Length > maxTokens) continue;

                var stateKey = SerializeTokens(newTokens);

                // Evita estados repetidos para prevenir ciclos infinitos y trabajo redundante
                if (visitedStates.Contains(stateKey)) continue;

                visitedStates.Add(stateKey);
                queue.Enqueue((newTokens, depth + 1));
            }
        }

        /// <summary>
        /// Crea una nueva secuencia de tokens reemplazando el token en 'index' por 'replacement'.
        /// </summary>
        private static string[] BuildExpandedTokens(string[] tokens, int index, string[] replacement)
        {
            // Uso de LINQ Concatenar para una construcción clara del nuevo array.
            var prefix = tokens.Take(index);
            var suffix = tokens.Skip(index + 1);
            return prefix.Concat(replacement).Concat(suffix).ToArray();
        }

        /// <summary>
        /// Serializa una secuencia de tokens en un string para usarlo como clave de estado.
        /// </summary>
        private static string SerializeTokens(IEnumerable<string> tokens)
        {
            // Usar string.Join es la forma más clara.
            return string.Join(" ", tokens).Trim();
        }

        /// <summary>
        /// Determina si un token es un símbolo no terminal (es decir, tiene producciones).
        /// </summary>
        private bool IsNonTerminal(string token) => _productions.ContainsKey(token);

        // ============================================================
        // == MEMBERSHIP VALIDATION (VALIDACIÓN DE PERTENENCIA)
        // ============================================================

        /// <summary>
        /// Verifica si la palabra dada pertenece al lenguaje generado.
        /// NOTA: Esta función sólo es útil si la palabra ya fue generada por GenerateWords.
        /// Para una validación completa, se requiere un algoritmo de parsing como CYK o LL/LR.
        /// </summary>
        public bool WordBelongs(string word, List<string> generatedWords) // Renombrado 'generated' a 'generatedWords'
        {
            if (string.IsNullOrWhiteSpace(word))
                return false;

            var normalizedWord = NormalizeWord(word); // Renombrado 'normalized' a 'normalizedWord'
            return generatedWords.Contains(normalizedWord);
        }

        /// <summary>
        /// Normaliza la palabra eliminando múltiples espacios en blanco.
        /// </summary>
        private static string NormalizeWord(string word)
        {
            // Uso de LINQ más moderno para limpiar y unificar el espaciado
            return string.Join(" ", word.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }
    }
}