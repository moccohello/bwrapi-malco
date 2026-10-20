# Malco Client

StarCraft: Brood War 오버레이와 launcher를 제공한다. 게임 상태는 BWRAPI SDK에서 읽고 Windows .NET Desktop Runtime으로 표시한다.

## 기능

- 유닛·건물·worker와 자원 채취 현황
- 연구·업그레이드 진행과 남은 시간
- 선택 유닛의 현재·예약 명령 경로와 건물 rally point
- transport 탑승 정보와 spell readiness
- 해처리·레어·하이브 아래 라바 생성까지 남은 게임 시간
- 에그·코쿤 위 변태 대상 유닛 아이콘
- 표시 항목·아이콘·HUD 위치·크기 설정

SDK는 `BwrApi.Client 8.0.0-embedded-preview.7`(ABI8)이다. 라바 생성 시간은 해처리·레어·하이브의
`RemainingTrainFrames`, 라바의 `HasHatcheryParent`·`HatcheryUnitTag`를 사용한다. ABI8 원천은
완료된 해처리 계열과 확인된 해처리→레어·레어→하이브 변태 중에 라바 타이머를 발행한다
(이전 `larva.3` runtime 수정과 같은 범위). ABI8 runtime의 실제 게임 값은 아직 확인하지 않았다.
24프레임을 게임 시간 1초로 환산해 올림 표시하며, 벽시계로 따로 감소시키지 않는다.
언어와 관계없이 숫자와 `s`만 표시한다(예: `1s`).
같은 건물에 연결된 라바가 3마리 이상이거나 남은 프레임이 0이면 숫자를 숨긴다.
타이머를 읽을 수 없을 때도 숨긴다.
라바 개수는 해당 게시본에 관측된 부모 관계 기준이며, 미확인 관계를 0마리로 처리하지 않는다.
환각 여부가 미확인인 라바가 있으면 개수도 미확인으로 둔다.
설정의 ‘라바 생성 시간’에서 켜고 끌 수 있으며 기본값은 켜짐이다.
숫자는 해처리·레어·하이브 영역 안쪽 하단에 배치한다.

변태 대상 아이콘은 같은 SDK의 `BuildTypeId`를 사용하며 SDK·runtime을 추가로 변경하지 않는다.
에그는 저글링·히드라리스크·울트라리스크·드론·오버로드·뮤탈리스크·퀸·디파일러·스커지,
코쿤은 가디언·디바우러의 기존 컬러 아이콘을 에그·코쿤 중앙에 겹쳐 표시한다.
28px 아이콘에 짙은 배경·밝은 회색 테두리·그림자를 적용해 가시성을 높인다. 목표를 읽을 수 없거나
목표 종류가 맞지 않거나 아이콘이 없으면 숨긴다. 설정의 ‘변태 대상 아이콘’에서 켜고 끌 수 있고
기본값은 켜짐이다. 완료 후 에그·코쿤 형태가 사라지면 아이콘도 제거한다.
카메라는 전체 상태 게시본의 40ms 캐시를 거치지 않는 별도 원천 관측으로 갱신한다.
화면 경계를 벗어난 변태 아이콘은 가장자리에 붙잡아 두지 않고 숨긴다.
테두리는 중립 회색이며, Shift+Tab의 현재 표시 색상을 뜻하지 않는다.

ABI8은 선택·환각·명령 대상과 유닛 목록 가용성을 미확인(null)으로 구분한다. 확인된 환각만 제외하고,
유닛 목록이 제공되지 않거나 원천이 일시적으로 없으면(code 4) 마지막 값을 유지한 채 대기로 표시한다.
`packages.lock.json`의 contentHash는 SDK 소스에서 로컬로 pack한 preview7 기준이다. 다른 빌드의
preview7 패키지를 쓰면 restore가 hash 불일치로 거부되므로 lock file을 다시 생성한다.
preview7은 보안 전용 검사를 제거한 runtime으로 로컬 pack했다(load 시 hash 대조 없음).

연구·업그레이드 알림은 현재 건물 조건으로 열린 기회를 표시한다. 자원·모든 명령 조건을 검사한 즉시 실행 보장은 아니다. 맵에서 금지된 연구와 도달한 맵별 최대 업그레이드는 표시하지 않으며, 생산 건물의 동력·애드온 연결을 확인하지 못하거나 건물이 사용 중이면 회색 차단 상태로 표시한다. 미확인 값은 금지로 단정하지 않는다.

관찰 호스트가 응답하지 않으면 마지막 수치는 보존하지만 수집 상태는 대기로 바뀐다. 필수 업데이트가 시작에 실패해 롤백한 경우에는 다음 오프라인 실행에서도 구버전 실행을 막는다. `malco.install-state.v2`의 `last_rollback.update_requirement` 선택 필드에 실패 당시 정책을 보존하고 후보의 서명 자료도 정리 대상에서 제외한다. 필드가 없는 기존 상태는 읽을 수 있으며, 서명 자료까지 없으면 정책 확인 전까지 구버전을 실행하지 않는다. Setup은 기존 설치 표식이 없는 새 설치에서만 패키지의 초기 상태를 복사하고, 표식이 있는 재설치에서는 상태 파일이 없더라도 초기화하지 않는다. 존재하는 상태 파일은 내용이 손상된 경우에도 덮어쓰지 않으며, launcher가 유효성 검사에 실패하면 실행을 중단한다.

첫 관찰 연결에서 SDK가 프로세스 조회·접근 불가를 보고하면 같은 게임 PID·생성 시각을 유지하며 250ms 간격으로 최대 10초 재시도한다. 계속 실패하면 마지막 오류를 포함한 연결 시간 초과를 표시하고 수집을 중단한다. 파일 무결성·ABI·native 생성 오류와 게임 프로세스 identity 변경은 자동 재시도하지 않는다. 이 재시도가 모든 게임 초기 미준비 상태를 처리한다는 보장은 아니다.

## 코드와 시작 위치

1.0.10 Setup은 재설치 때 기존 상태·HUD 설정을 유지하고 패키지의 새 버전을 업데이트
후보로 준비한다. 기존 후보가 있으면 보존한다. 1.0.9 자동 업데이트의 용량 한도 때문에
1.0.10 전환에는 새 Setup을 안내한다. 정식 policy는 `packaging/launcher-policy.json`이며
payload 32 MiB·archive 16 MiB를 허용한다.

- `src/Malco/`: 오버레이 앱
- `src/Malco.Launcher/`: 제품 실행 진입점
- `src/Malco.Telemetry/`: 설치·시작 telemetry 전송
- `packaging/`: Windows 설치 파일
- `create-release-payload.ps1`, `package-installer.ps1`: release 산출물 생성
- 서버 등록·공개 절차는 [Malco Server](../../malco/server/README.md)를 따른다.

`package-installer.ps1`에는 `-ReleaseToolPath <Malco.ReleaseTool.exe>`도 지정한다. launcher policy 입력은 launcher와 ReleaseTool이 정확히 21개 키만 허용하는 닫힌 `malco.launcher-policy.v2` 계약으로 검증한다. compiler 실행 전에 복사한 envelope·payload·launcher policy를 ReleaseTool로 검증한다. 생성된 `Malco-{version}-Setup.exe`와 `Malco-{version}-Setup.binding.json`을 함께 서버의 `packages/{version}/`에 등록한다. binding은 정상 패키징한 파일들의 hash·길이·version·sequence를 묶는 영수증이며, Setup 내부 payload에 대한 별도 서명 증명은 아니다. 기존 Setup에 binding을 손으로 추가하지 말고 검증된 입력으로 다시 패키징한다.

두 출력 파일은 installer별 `.publish.lock`으로 예약한다. binding 이동이 실패하면 이번 시도에서 이동한 동일 installer만 되돌려 재시도할 수 있게 한다. 비정상 종료로 lock이 남았다면 해당 패키징 프로세스가 종료했음을 확인한 뒤 그 lock만 제거한다. 이미 출력된 파일은 임의로 지우지 않는다.
