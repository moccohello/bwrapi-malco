# Malco Client 작업 지침

이 저장소는 오버레이, 런처, 패키징 스크립트를 소유한다.
공개 웹, telemetry API, 서명 도구, 라이브 게시는 형제 저장소 `../server`가 소유한다.

- 먼저 README와 직접 고칠 코드만 읽는다.
- 게임 쓰기는 하지 않는다. BWRAPI는 read/query only다.
- 빌드·픽스처를 실동작 증거로 말하지 않는다.

## 출시 핸드오프

제품 코드를 바꾼 뒤 공개하려면 이 순서를 지킨다. 스크립트 인자는 해당 `.ps1`를 본다.

1. `src/Malco/Malco.csproj`, `src/Malco.Launcher/Malco.Launcher.csproj`의 `Version`과 README 타임라인을 같은 버전으로 맞춘다.
2. 클라이언트에서 payload zip을 만든다 (`create-release-payload.ps1`).
3. 서버 `ReleaseTool create`로 `latest.json`을 서명한다. `--previous-latest`는 현재 공개 피드, `--release-sequence`는 그보다 큰 값, `--release-base-url`은 `https://update.horangmalco.com/releases/{version}/` 이다.
4. 설치 파일을 만든다 (`package-installer.ps1`).
5. 서버 `deploy/register-release-package.ps1`로 `../server/packages/{version}/`에 `latest.json`, `payload.zip`, `Malco-{version}-Setup.exe`를 함께 등록한다. 선택 `notes.json`이 있으면 같은 폴더에 둔다.
6. 서버 `deploy/publish-release.ps1`의 `PublicationRoot`는 **`/var/lib/malco/publication`만** 쓴다.

라이브 확인은 사이트 다운로드 exe와 `https://update.horangmalco.com/latest.json`이다. 둘 다 방금 올린 버전이어야 한다.

## 하지 말 것

- `../server/publication/`에 복사하지 않는다. 그 경로는 git에서 제외했고, Caddy가 서빙하지 않는다.
- `packages/`나 게시 바이너리를 git에 넣지 않는다.
- zip만 올리거나 installer만 올리지 않는다. 한 버전은 런처용 `releases/`와 웹용 `installers/`가 같이 있어야 한다.
- 서명 키, 런처 정책, `.malco-win-release-*`를 커밋하지 않는다.
