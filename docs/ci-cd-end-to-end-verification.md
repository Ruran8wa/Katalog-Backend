# CI/CD End-to-End Verification

This guide verifies the pipeline from pull request validation through publishing an image to GitHub Container Registry (GHCR). Run the commands from a clean clone with GitHub CLI (`gh`), Docker, and permission to push branches and merge pull requests.

## Prerequisites

Configure these repository Actions secrets before opening the pull request:

| Secret | Value |
| --- | --- |
| `CI_DB_PASSWORD` | Password used by the ephemeral SQL Server container |
| `CI_SERVICE_BUS_CONNECTION_STRING` | Connection string for the CI Service Bus emulator |

The Service Bus secret must point to the local emulator used by CI, not a shared Azure namespace. For the committed emulator configuration, its value is typically supplied by the repository administrators. Never put either value in a workflow file, command committed to the repository, or log output.

GHCR publishing uses the built-in `GITHUB_TOKEN`; no registry password is needed in the workflow. To pull a private package locally, authenticate Docker with a GitHub token that has `read:packages`:

```bash
export GHCR_TOKEN='...'
echo "$GHCR_TOKEN" | docker login ghcr.io -u "$GITHUB_USER" --password-stdin
```

## 1. Open the pull request

Push the pipeline branch and create a pull request targeting `main`:

```bash
git switch workflow-pipeline
git status
git push --set-upstream origin workflow-pipeline
gh pr create \
  --base main \
  --head workflow-pipeline \
  --title "Add CI/CD pipeline" \
  --body "Validates build, tests, migrations, Docker images, and GHCR publishing."
```

Use the repository pull-request template when filling in the body. Confirm that the pull request shows the required `build-and-test` check. Since `main` is protected, it cannot be merged while that check is failing or incomplete.

## 2. Watch the pull-request workflow

Watch the checks from the command line:

```bash
gh pr checks --watch
```

The `build-and-test` job should:

1. Restore packages from the NuGet cache.
2. Build the solution in Release mode.
3. Fail if EF Core has pending model changes.
4. Build the application, migrator, consumer, and Service Bus emulator images.
5. Start the ephemeral SQL Server, SQL Edge, and Service Bus emulator containers.
6. Apply EF migrations.
7. Run the complete NUnit suite and upload `test-results`.
8. Tear down the containers and volumes.

The `publish` job must be skipped for this pull request. It only runs after a successful job on a push to `main` or a `v*` tag.

Inspect a failed run and its logs with:

```bash
gh run list --workflow build-test.yaml --limit 5
gh run view RUN_ID --log-failed
gh run download RUN_ID --name test-results
```

Do not merge until `build-and-test` is green and the test-results artifact is present.

## 3. Merge and verify the main-branch image

Merge only after the required check passes:

```bash
gh pr merge PR_NUMBER --squash --delete-branch
```

Watch the new push workflow:

```bash
gh run watch RUN_ID
```

Find the merge commit and derive the expected short SHA:

```bash
MERGE_SHA="$(gh api repos/Ruran8wa/Katalog-Backend/commits/main --jq '.sha')"
SHORT_SHA="${MERGE_SHA:0:7}"
IMAGE="ghcr.io/ruran8wa/katalog-backend"
echo "$MERGE_SHA"
echo "$SHORT_SHA"
```

The successful `publish` job must push the image tagged with:

```text
sha-SHORT_SHA
```

For example, a commit beginning with `a1b2c3d` is published as `sha-a1b2c3d`. The workflow intentionally does not rely on `latest`.

Confirm the package and tag through GitHub:

```bash
gh api \
  repos/Ruran8wa/Katalog-Backend/packages/container/katalog-backend/versions \
  --jq '.[].metadata.container.tags[]' | sort -u
```

Confirm the expected tag is present:

```bash
docker manifest inspect "$IMAGE:sha-$SHORT_SHA"
```

If the package is private, run the GHCR login from the prerequisites first.

## 4. Verify `/version` from the published image

Pull and run the exact SHA-tagged image. Disable background workers because this check is only verifying the image’s version endpoint and does not start the local dependency stack:

```bash
docker pull "$IMAGE:sha-$SHORT_SHA"
docker run --rm -d \
  --name katalog-version-check \
  -p 18080:8080 \
  -e BackgroundServices__ConsumerEnabled=false \
  -e BackgroundServices__OutboxPublisherEnabled=false \
  "$IMAGE:sha-$SHORT_SHA"
```

Wait for the HTTP endpoint and inspect the response:

```bash
for attempt in $(seq 1 30); do
  if curl --fail --silent http://localhost:18080/version; then
    echo
    break
  fi
  sleep 2
done

VERSION_RESPONSE="$(curl --fail --silent http://localhost:18080/version)"
echo "$VERSION_RESPONSE"
test "$(printf '%s' "$VERSION_RESPONSE" | jq -r '.version')" = "$MERGE_SHA"
docker rm --force katalog-version-check
```

The response must contain the full merge commit SHA:

```json
{"version":"<merge-commit-sha>"}
```

## 5. Verify a version tag

After the main-branch image is verified, create and push a release tag at the same commit:

```bash
VERSION="v1.2.0"
git fetch origin main
git tag "$VERSION" origin/main
git push origin "$VERSION"
```

Watch the tag-triggered workflow:

```bash
gh run list --workflow build-test.yaml --limit 3
gh run watch TAG_RUN_ID
```

Confirm both tags exist:

```bash
docker manifest inspect "$IMAGE:sha-$SHORT_SHA"
docker manifest inspect "$IMAGE:$VERSION"
```

The version-tagged image must report the same full commit SHA at `/version`:

```bash
docker pull "$IMAGE:$VERSION"
docker run --rm -d \
  --name katalog-version-check \
  -p 18080:8080 \
  -e BackgroundServices__ConsumerEnabled=false \
  -e BackgroundServices__OutboxPublisherEnabled=false \
  "$IMAGE:$VERSION"

curl --fail --silent http://localhost:18080/version
docker rm --force katalog-version-check
```

Both the SHA tag and `v1.2.0` must identify the commit that was tested and merged. Remove a local tag only if the release tag was created accidentally; do not overwrite a published version tag.
