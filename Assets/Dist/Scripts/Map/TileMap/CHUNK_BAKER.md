# RhombusChunkBaker — 콜라이더 베이킹

자식 오브젝트의 `RhombusTileMarker` 들을 스캔해
**Largest-Rect-First** 알고리즘으로 인접 타일을 최대 직사각형으로 병합,
단일 `MeshCollider` 로 베이킹한다.

---

## 처리 흐름

```
RhombusTileMarker[] (자식 오브젝트)
        │
        ▼
① 로컬 좌표 변환 + 그리드 스냅 (FloorToInt)
        │
        ▼
② Y 레이어별 그룹화
        │
        ▼  (레이어마다 반복)
③ bool[cols, rows] 그리드 구성
        │
        ▼
④ FindLargestRect → 가장 큰 직사각형 추출
        │         ← 추출된 영역 grid에서 제거
        └──────────────── 타일 남으면 반복
        │
        ▼
⑤ 직사각형마다 AddBox (8 verts, 12 tris)
        │
        ▼
⑥ Mesh 빌드 → MeshCollider 적용
```

---

## 1단계 — 그리드 좌표 변환

타일 월드 위치를 `(gx, gz)` 정수 좌표로 변환한다.

```
gx = FloorToInt(localX / diagX)
gz = FloorToInt(localZ / diagZ)
```

### RoundToInt 를 쓰면 안 되는 이유

이 프로젝트의 타일은 반정수 위치 `(N + 0.5)` 에 배치된다.
`Mathf.RoundToInt` 는 C# 기본 뱅커 반올림(ToEven) 을 사용하므로
인접한 두 타일이 동일한 gx 에 충돌한다.

```
위치   / diagX    RoundToInt    FloorToInt
─────────────────────────────────────────
-4.5  →  -4.5  →    -4   ←── 충돌!     -5
-3.5  →  -3.5  →    -4   ←── 충돌!     -4   ← 인접(차이=1) ✓
-2.5  →  -2.5  →    -2   ←── 충돌!     -3
-1.5  →  -1.5  →    -2   ←── 충돌!     -2   ← 인접(차이=1) ✓
-0.5  →  -0.5  →     0              -1
 0.5  →   0.5  →     0   ←── 충돌!      0   ← 인접(차이=1) ✓
```

`FloorToInt` 는 `[N × diagX, (N+1) × diagX)` 구간 전체를 gx=N 으로 취급하므로
반정수 위치 타일이 항상 서로 다른, 연속된 gx 를 갖는다.

---

## 2단계 — Y 레이어 분리

```
layerKey = RoundToInt(localY / thicknessY)
```

서로 다른 높이의 타일(계단, 고저차 지형)을 레이어별로 독립 처리한다.
Y 는 타일 중심 높이라 반정수 문제가 없으므로 `RoundToInt` 를 사용한다.

---

## 3단계 — FindLargestRect (Largest Rectangle in Histogram)

### 핵심 아이디어

2D bool 그리드를 행(row) 단위로 스캔하면서,
"이 행까지 위로 연속된 타일 수" 를 `heights[]` 배열로 관리한다.

```
그리드:               heights (row=2 시점):
row 0: [T][T][T]       [1][1][1]
row 1: [T][T][ ]  →   [2][2][0]
row 2: [T][T][T]       [3][3][1]
                        └─┴─ 이 histogram에서 최대 rect = 2×2 (면적 4)
```

각 행마다 histogram 최대 직사각형 탐색 (스택 기반, **O(cols)**).

### 스택 알고리즘 상세

```
heights = [3, 2, 1]  (예시)

col=0: h=3, 스택 비어있음 → push(0)         stack=[0]
col=1: h=2, heights[0]=3 > 2 → pop(0)
         height=3, startCol=0, width=1, area=3  ← 후보
       h=2, 스택 비어있음 → push(1)           stack=[1]
col=2: h=1, heights[1]=2 > 1 → pop(1)
         height=2, startCol=0, width=2, area=4  ← 최대
       h=1, 스택 비어있음 → push(2)           stack=[2]
sentinel(h=0): pop(2)
         height=1, startCol=0, width=3, area=3
→ 최대 직사각형: 면적 4 (width=2, height=2)
```

팝 시점에 직사각형의 행 시작 위치는:
```
rz = row - height + 1
```

### 전체 반복

```
while (grid 에 타일 남음):
    FindLargestRect → (rx, rz, rw, rh) 반환
    grid[rx..rx+rw, rz..rz+rh] = false  // 해당 영역 제거
    AddBox(...)                           // 박스 메시 추가
```

---

## 4단계 — 월드 중심 & 크기 계산

FloorToInt 기준으로 `gx=N` 인 타일은 월드 구간 `[N·diagX, (N+1)·diagX]` 를 커버한다.
따라서 `rw` 개 타일로 이루어진 직사각형의 중심과 반폭은:

```
중심X  = (minX + rx + rw × 0.5) × diagX
반폭hw = rw × diagX × 0.5

중심Z  = (minZ + rz + rh × 0.5) × diagZ
반깊hd = rh × diagZ × 0.5
```

예) `minX=-5, rx=0, rw=4, diagX=1`
→ 중심X = (-5 + 0 + 2) × 1 = **-3.0**
→ 타일 중심 평균: (-4.5 + -3.5 + -2.5 + -1.5) / 4 = **-3.0** ✓

---

## 5단계 — AddBox

박스 하나 = **8 vertices, 12 triangles**

```
윗면:  v0(-x,+y,+z)  v1(+x,+y,+z)  v2(+x,+y,-z)  v3(-x,+y,-z)
아랫면: v4(-x,-y,+z)  v5(+x,-y,+z)  v6(+x,-y,-z)  v7(-x,-y,-z)

면 구성 (반시계 = 바깥 법선):
  윗면:    0-1-2, 0-2-3
  아랫면:  6-5-4, 7-6-4
  앞면:    0-4-5, 0-5-1
  오른쪽:  1-5-6, 1-6-2
  뒤쪽:    2-6-7, 2-7-3
  왼쪽:    3-7-4, 3-4-0
```

---

## 성능 특성

| 항목 | 값 |
|------|----|
| 시간 복잡도 | O(k × cols × rows), k = 최종 직사각형 수 |
| 공간 복잡도 | O(cols × rows) — bool 그리드 |
| verts / rect | 8 고정 |
| tris / rect  | 12 고정 |

### 병합 효과 예시

| 배치 | 원래 verts | 병합 후 verts |
|------|-----------|--------------|
| 8×8 정사각형 | 512 | 8 (rect 1개) |
| L자 10타일   | 80  | ~24 (rect 3개 이하) |
| 완전 불규칙   | 8N  | 병합 불가 타일 × 8 |

---

## MeshCollider 쿠킹 옵션

```csharp
MeshColliderCookingOptions.EnableMeshCleaning      // 중복/퇴화 폴리곤 제거
| MeshColliderCookingOptions.WeldColocatedVertices  // 동위치 정점 병합
| MeshColliderCookingOptions.UseFastMidphase        // BVH 가속 (정적 환경 최적)
convex = false  // 오목 메시 허용 (런타임 Rigidbody 충돌 불가, 정적 전용)
```

---

## 인스펙터 설정

| 필드 | 설명 | 기본값 |
|------|------|--------|
| `diagX` | 타일 가로 크기 (월드 단위) | 1 |
| `diagZ` | 타일 세로 크기 (월드 단위) | 1 |
| `thicknessY` | 콜라이더 두께 | 0.05 |
| `includeInactive` | 비활성 자식 포함 여부 | false |

> `diagX` / `diagZ` 는 **실제 타일 배치 간격과 일치**해야 한다.
> 불일치 시 그리드 스냅이 어긋나 병합이 일어나지 않는다.

---

## 컨텍스트 메뉴

| 메뉴 | 동작 |
|------|------|
| **Bake Chunk** | 전체 베이킹 실행 |
| **Debug: Print Tile Grid** | 각 타일의 로컬 좌표 · gx/gz 출력 (병합 문제 진단용) |
| **Clear Baked Mesh** | 메시 초기화 |

---

## 주의 / 제약

- `#if UNITY_EDITOR` — 에디터 전용. 런타임 빌드에 포함되지 않음.
- Bake 결과는 **씬에 인라인 메시로 저장**된다. 씬 저장 필요.
- 타일이 겹치거나(동일 gx/gz) 부동소수점 오차가 크면 grid 가 깨질 수 있다.
  → "Debug: Print Tile Grid" 로 gx/gz 확인 후 diagX/diagZ 조정.
- Y 레이어가 다른 타일끼리는 병합되지 않는다 (의도된 동작).
