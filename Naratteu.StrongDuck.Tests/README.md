# Naratteu.StrongDuck 테스트 프로젝트

```bash
dotnet test
```

커버리지 수집:

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory .coverage
```

리포트는 `.coverage/<guid>/coverage.cobertura.xml` 경로에 생성됩니다.

참고 링크:

- https://learn.microsoft.com/dotnet/core/tools/dotnet-test
- https://learn.microsoft.com/dotnet/core/testing/unit-testing-code-coverage
