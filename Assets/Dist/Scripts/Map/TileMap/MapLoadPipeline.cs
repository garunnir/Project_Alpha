namespace IsoTilemap
{
    public sealed class MapLoadPipeline
    {
        private readonly IMapSerializer _serializer;
        private readonly IMapModelBuilder _modelBuilder;
        private readonly IMapMapper _mapper;

        public MapLoadPipeline(IMapSerializer serializer,
                               IMapModelBuilder modelBuilder,
                               IMapMapper mapper)
        {
            _serializer = serializer;
            _modelBuilder = modelBuilder;
            _mapper = mapper;
        }

        public IMapModel LoadModel(string path)
        {
            // Deserialize JSON to DTO
            MapSaveJsonDto dto = _serializer.Read(path);
            // Map DTO to Domain Model
            MapModelDTO prepared = _mapper.ToPrepared(dto);
            // Build Runtime Data
            return _modelBuilder.Build(prepared);
        }
    }
}
