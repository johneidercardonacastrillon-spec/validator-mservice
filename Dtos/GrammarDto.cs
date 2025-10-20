namespace ValidatorService.Dtos
{
    public class GrammarDto
    {
        public string Id { get; set; }
        public string StartSymbol { get; set; }
        public List<ProductionDto> Productions { get; set; }
    }
}
