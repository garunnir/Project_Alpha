using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace IsoTilemap
{
    //맵 도메인 모델 빌더 담당
    /*
    ① 스냅샷 → 모델 변환

- JSON / DTO / SO → **불변 모델(Definition)**
- 구조를 바꿔도 스냅샷과 분리 가능


② 정합성 검증 (Validation)

빌더에서 잡아야 할 것들:

- 중복 좌표
- 범위 밖 타일
- 없는 타일 타입 참조
- 룸 ID 불일치

```
“게임이 시작된 후에는 절대 터지면 안 되는 것들”

👉 런타임에서 체크하면 이미 늦다.

---

③ 정규화 / 보정 (Normalize)

예시:

- 누락된 타일 자동 채움
- 정렬 (Bounds 계산)
- 기본값 삽입
- 좌표계 변환


④ 런타임 최적화용 캐시 생성


- Dictionary / Grid / Chunk 분할
- Neighbors 캐시
- 룸별 타일 인덱스
- 빠른 조회 테이블
    */
    public class TileMapModelBuilder : IMapModelBuilder
    {

        public IMapModel Build(IMapTilesReadOnly prepared)
        {
            TileMapModel data = new TileMapModel(prepared);

            return data;
        }

    }
}


