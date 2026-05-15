namespace IsoTilemap
{
    public readonly struct MapLoadResult
    {
        public MapSaveJsonDto Dto { get; }
        public IMapModel Model { get; }

        public MapLoadResult(MapSaveJsonDto dto, IMapModel model)
        {
            Dto = dto;
            Model = model;
        }
    }

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

        public MapLoadResult Load(string path)
        {
            MapSaveJsonDto dto = _serializer.Read(path);
            if (dto == null)
                throw new System.InvalidOperationException($"[MapLoadPipeline] 맵 파일 로드 실패: {path}");

            MapModelDTO prepared = _mapper.ToPrepared(dto);
            if (prepared == null)
                throw new System.InvalidOperationException($"[MapLoadPipeline] DTO 변환 실패: {path}");

            return new MapLoadResult(dto, _modelBuilder.Build(prepared));
        }

        public IMapModel LoadModel(string path) => Load(path).Model;
    }
}
