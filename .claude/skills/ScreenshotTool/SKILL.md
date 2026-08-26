```markdown
# ScreenshotTool Development Patterns

> Auto-generated skill from repository analysis

## Overview
This skill teaches best practices for contributing to the ScreenshotTool C# codebase. You'll learn the project's coding conventions, commit patterns, and how to write and run tests. The repository is a C# application with no detected framework, following clear naming and import/export standards. Commit messages use the conventional commit style, and tests follow a specific naming pattern.

## Coding Conventions

### File Naming
- Use **PascalCase** for all file names.
  - Example: `ScreenshotManager.cs`, `ImageProcessor.cs`

### Import Style
- Use **relative imports** for referencing other files or namespaces within the project.
  - Example:
    ```csharp
    using ScreenshotTool.Utilities;
    ```

### Export Style
- Use **named exports** for classes and methods.
  - Example:
    ```csharp
    public class ScreenshotManager
    {
        public void Capture() { ... }
    }
    ```

### Commit Messages
- Follow the **conventional commit** pattern.
- Prefixes: `feat`, `fix`
- Example:
  - `feat: add region selection for screenshots`
  - `fix: correct file save path issue`

## Workflows

### Add a New Feature
**Trigger:** When implementing a new functionality.
**Command:** `/add-feature`

1. Create a new file using PascalCase (e.g., `NewFeature.cs`).
2. Use relative imports for dependencies.
3. Export your class or method with a named export.
4. Write a commit message starting with `feat:`.
5. Example commit: `feat: implement delay timer for screenshots`

### Fix a Bug
**Trigger:** When resolving a bug or issue.
**Command:** `/fix-bug`

1. Locate the relevant file(s) and make necessary changes.
2. Use relative imports if adding new dependencies.
3. Write a commit message starting with `fix:`.
4. Example commit: `fix: resolve crash on empty clipboard`

### Write and Run Tests
**Trigger:** When adding or updating tests.
**Command:** `/run-tests`

1. Create or update test files following the `*.test.*` naming pattern (e.g., `ScreenshotManager.test.cs`).
2. Implement test cases for your features or bug fixes.
3. Use the project's preferred test runner (framework unknown; check project docs or scripts).
4. Run the tests and ensure they pass before committing.

## Testing Patterns

- Test files are named with the pattern `*.test.*` (e.g., `ImageProcessor.test.cs`).
- Place test files alongside the code they test or in a dedicated test directory.
- The test framework is not specified; consult project documentation or maintainers for details.
- Example test file:
  ```csharp
  // ScreenshotManager.test.cs
  using ScreenshotTool;
  using Xunit;

  public class ScreenshotManagerTests
  {
      [Fact]
      public void Capture_SavesFile()
      {
          // Arrange
          var manager = new ScreenshotManager();
          // Act
          manager.Capture();
          // Assert
          // ...assert file exists...
      }
  }
  ```

## Commands
| Command        | Purpose                                      |
|----------------|----------------------------------------------|
| /add-feature   | Start the workflow for adding a new feature  |
| /fix-bug       | Start the workflow for fixing a bug          |
| /run-tests     | Run all tests in the codebase                |
```