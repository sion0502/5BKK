# 약제실 — 기존 병원 에셋 적용 완료

프리팹: `Assets/Prefab/Map/Tile/Chapter1/Tile_3Way (Dispensary).prefab`

이전의 단순 도형 인테리어를 제거하고 `Storage`, `Ward`, `OperatingRoom`에서 사용하는 The Horror Hospital 에셋으로 교체했다. 사용자가 별도로 모델이나 재질을 붙일 필요 없다.

## 직접 적용한 에셋

- HH_Glass_Medical_Cabinet: 유리 약품장 6개
- HH_ Base_Cabinet_2m_01: 조제 캐비닛 6개
- HH_Public_Seating_02: 대기 좌석 4개
- HH_Medical_Trolley: 의료 카트 2개
- HH_Cooler_02 / HH_Medical_Waste: 정수기와 의료 폐기물함
- HH_Cardboard_Box_07 / HH_Medical_Box_01 / HH_Registrar_Folder_01: 보급 상자, 의료 상자, 환자 기록
- HH_Flooring_Concrete_4x4m_01: 병원 바닥 모듈
- HH_Hospital_PartitionWall_4x4m: 병원 벽 마감 모듈

모델에 연결된 원래 재질·텍스처·LOD를 사용한다. 총 11종, 388개 프리팹 인스턴스이며 대부분은 바닥과 벽 모듈이다. 실제 가구·보급품은 28개, 카운터 위 장식 소품은 12개다.

## 유지한 조건

Store 타일의 모든 기존 Transform과 출입구 인스턴스를 비교하여 위치·회전·크기가 바뀌지 않았음을 확인했다. 기존 벽과 바닥 충돌은 유지하고, 단순 벽·바닥의 렌더러만 가려 같은 위치를 병원 모델로 마감했다. 문과 중앙 이동 경로는 유지했다. 연결 방향은 Down | Left | Right (14)다.

가구는 원본 프리팹의 자식 콜라이더까지 계산해 바닥에 맞추어 배치했다. 중앙 통로와 내부 벽 경계를 침범하지 않는 좌표 검사를 통과했다. 기존 병원 원본 에셋은 수정하지 않았다.

## 사용 및 검증

Unity에서 위 프리팹을 열면 실제 에셋이 적용된 상태로 확인할 수 있다. 자동 생성 테스트용 목록은 `Assets/Scripts/Map/Script/Chapter1 Tiles - Dispensary Preview.asset`이며 기존 제작용 타일 목록은 유지했다.

새 타일과 참조하는 원본 프리팹 11종의 모델·재질 GUID 존재 여부, 로컬 참조, 중복 ID를 검사했다. Unity 화면 렌더링 및 NavMesh 이동 테스트는 아직 실행하지 않았다. `applied-assets.json`에는 모든 적용 에셋과 위치·배율이 기록되어 있다. 이전 도형 배치도는 현재 결과와 달라 제거했다.
