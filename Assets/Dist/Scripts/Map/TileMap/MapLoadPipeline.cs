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
            MapSaveJsonDto dto = _serializer.Read(path);
            if (dto == null)
                throw new System.InvalidOperationException($"[MapLoadPipeline] 맵 파일 로드 실패: {path}");

            MapModelDTO prepared = _mapper.ToPrepared(dto);
            if (prepared == null)
                throw new System.InvalidOperationException($"[MapLoadPipeline] DTO 변환 실패: {path}");

            return _modelBuilder.Build(prepared);
        }
    }
}
