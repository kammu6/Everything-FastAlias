const fs = require("fs");
const path = require("path");

const OVERVIEW_PATH = path.join("docs", "memories", "overview.md");

const IGNORE_DIRS = new Set([
  ".git",
  "node_modules",
  "temp",
  ".history",
  ".agents",
  "screenshots",
  "snapshots",
  "tavily",
  "evaluate_scripts",
  "websavers",
  "manuals",
  ".venv",              // Python virtual env
  "__pycache__",        // Python compilation cache
  "bin",                // C# Build Artifacts
  "obj",                // C# Build Artifacts
  ".vs",                // Visual Studio cache
  "zip-tree",           // Zip backups
  "TestResults",        // MSTest execution artifacts
  "TempRunner"          // C# Temp project/runner folder
]);

const IGNORE_FILES = new Set([
  ".gitignore",
  "package-lock.json",
  "yarn.lock",
  "pnpm-lock.yaml",
  "poetry.lock",
  ".gitkeep",
  "build-debug.bat",
  "build-release.bat"
]);

// NOTE ON CUSTOMIZATION:
// 본 스크립트는 범용 뼈대(Skeleton)입니다. 프로젝트의 구조적 특성에 맞게 buildTree 내 필터를 커스텀하십시오.

function buildTree(dirPath, relativeDir = "") {
  const items = fs.readdirSync(dirPath, { withFileTypes: true });
  
  items.sort((a, b) => {
    if (a.isDirectory() && !b.isDirectory()) return -1;
    if (!a.isDirectory() && b.isDirectory()) return 1;
    return a.name.localeCompare(b.name);
  });

  const children = [];
  
  for (const item of items) {
    const name = item.name;
    const relPath = relativeDir ? `${relativeDir}/${name}` : name;
    
    if (item.isDirectory() && IGNORE_DIRS.has(name)) continue;
    
    // Ignore C# user files as well
    if (item.isFile() && (IGNORE_FILES.has(name) || name.endsWith(".user") || name.endsWith(".suo"))) {
      continue;
    }
    
    // Skip test files details to keep the tree clean
    if (item.isDirectory() && name === "tests") {
      children.push({
        name: name + "/",
        isDirectory: true,
        relPath: relPath,
        children: [] // No sub-children scan
      });
      continue;
    }
    
    if (relPath.startsWith("docs")) {
      const isDir = item.isDirectory();
      const inMemories = relPath.startsWith("docs/memories");
      const inBackup = relPath.startsWith("docs/memories/backup");
      
      if (inBackup) {
        if (relPath !== "docs/memories/backup") {
          continue; // backup 하위 파일 및 폴더 제외
        }
      }
      
      if (inMemories) {
        // memories 하위는 모두 통과 (단, backup 하위는 위에서 걸러짐)
      } else {
        const pathParts = relPath.split("/");
        const depth = pathParts.length;
        if (isDir && depth <= 2) {
          // 1차 하위 폴더만 통과
        } else {
          continue;
        }
      }
    }

    if (item.isDirectory()) {
      const subChildren = buildTree(path.join(dirPath, name), relPath);
      children.push({
        name: name + "/",
        isDirectory: true,
        relPath: relPath,
        children: subChildren
      });
    } else {
      children.push({
        name: name,
        isDirectory: false,
        relPath: relPath
      });
    }
  }
  
  return children;
}

function renderTree(nodes, comments, prefix = "", hasFixedPart = false) {
  let result = "";
  for (let i = 0; i < nodes.length; i++) {
    const node = nodes[i];
    const isRealLast = (i === nodes.length - 1);
    const isLast = isRealLast && !(prefix === "" && hasFixedPart);
    
    const marker = isLast ? "└── " : "├── ";
    const childPrefix = isLast ? "    " : "│   ";
    
    const emoji = node.isDirectory ? "📁 " : "📄 ";
    let line = `${prefix}${marker}${emoji}${node.name}`;
    
    const comment = comments[node.relPath];
    if (comment) {
      const padLen = Math.max(32, line.length);
      line = line.padEnd(padLen) + " " + comment;
    }
    
    result += line + "\n";
    if (node.isDirectory && node.children && node.children.length > 0) {
      result += renderTree(node.children, comments, prefix + childPrefix, false);
    }
  }
  return result;
}

function extractComments(treeText) {
  const comments = {};
  const stack = [];
  const lines = treeText.split(/\r?\n/);
  
  for (const line of lines) {
    if (!line.trim()) continue;
    if (line.includes("📂")) {
      stack.length = 0;
      continue;
    }
    const leadingMatch = line.match(/^([│\s]*?)(├──|└──)/);
    if (!leadingMatch) continue;
    
    const prefix = leadingMatch[1];
    const depth = Math.floor(prefix.length / 4);
    const rem = line.slice(leadingMatch[0].length).trim();
    
    let namePart = rem;
    let comment = "";
    if (rem.includes(" #")) {
      const idx = rem.indexOf(" #");
      namePart = rem.slice(0, idx).trim();
      comment = "# " + rem.slice(idx + 2).trim();
    }
    
    namePart = namePart.replace(/[📁📄🔒✏️📂]\s*/g, "").trim();
    namePart = namePart.replace(/\/$/, "");
    
    while (stack.length > depth) {
      stack.pop();
    }
    stack.push(namePart);
    const relPath = stack.join("/");
    
    if (comment) {
      comments[relPath] = comment;
    }
  }
  return comments;
}

function main() {
  console.log("=== Updating overview.md Directory Tree Programmatically ===");

  if (!fs.existsSync(OVERVIEW_PATH)) {
    console.error(`[ERROR] overview.md not found at: ${OVERVIEW_PATH}`);
    process.exit(1);
  }

  const overviewContent = fs.readFileSync(OVERVIEW_PATH, "utf8");
  
  let existingTreeText = "";
  const markerMatch = overviewContent.match(/<!--\s*START_TREE\s*-->\s*```(?:text)?\r?\n([\s\S]*?)\r?\n```\s*<!--\s*END_TREE\s*-->/i);
  if (markerMatch) {
    existingTreeText = markerMatch[1];
  } else {
    const fallbackMatch = overviewContent.match(/###?\s*3\.1\..*?\r?\n\s*```(?:text)?\r?\n([\s\S]*?)\r?\n```/i);
    if (fallbackMatch) {
      existingTreeText = fallbackMatch[1];
    }
  }

  if (!existingTreeText) {
    console.error("[ERROR] Failed to find existing tree structure in overview.md");
    process.exit(1);
  }

  const existingLines = existingTreeText.split(/\r?\n/);
  let screenshotStartIndex = -1;
  for (let i = 0; i < existingLines.length; i++) {
    if (existingLines[i].includes("screenshots/") && (existingLines[i].includes("├──") || existingLines[i].includes("└──"))) {
      screenshotStartIndex = i;
      break;
    }
  }

  let fixedPart = "";
  if (screenshotStartIndex !== -1) {
    // 주석 중복 방지를 위해 # (Note) 로 시작하는 라인은 필터링하여 수집
    const fixedLines = existingLines.slice(screenshotStartIndex)
      .filter(line => !line.trim().startsWith("# (Note)") && line.trim() !== "");
    fixedPart = fixedLines.join("\n");
    console.log("[INFO] Found fixed screenshots/ structure in existing tree (filtered comment).");
  } else {
    console.warn("[WARN] screenshots/ not found in existing tree. Proceeding without fixed part.");
  }

  const comments = extractComments(existingTreeText);
  console.log(`[INFO] Extracted ${Object.keys(comments).length} comments from existing tree.`);

  const treeNodes = buildTree(".", "");
  const hasFixedPart = fixedPart.length > 0;
  
  let renderedNewTree = renderTree(treeNodes, comments, "", hasFixedPart);
  const rootFolderName = path.basename(path.resolve("."));
  let finalTreeText = `📂 ${rootFolderName}\n` + renderedNewTree;
  if (hasFixedPart) {
    finalTreeText += fixedPart;
  }
  // 자동 안내 주석 추가
  finalTreeText += "\n\n# (Note) 에이전트의 토큰 절약을 위해 주요 폴더 구조와 memories 내 핵심 파일들만 선별하여 표시하고 있습니다.";

  let updatedOverviewContent = overviewContent;
  const commentRegex = /(<!--\s*START_TREE\s*-->\s*```text\r?\n)[\s\S]*?(\r?\n```\s*<!--\s*END_TREE\s*-->)/i;
  
  if (commentRegex.test(updatedOverviewContent)) {
    updatedOverviewContent = updatedOverviewContent.replace(commentRegex, `$1${finalTreeText}\n$2`);
    console.log("[SUCCESS] Updated directory tree using HTML comment markers.");
  } else {
    const fallbackRegex = /(###?\s*3\..*?\r?\n\s*)(?:<!--[\s\S]*?-->\s*)?(```text\r?\n)[\s\S]*?(\r?\n```)/i;
    if (fallbackRegex.test(updatedOverviewContent)) {
      updatedOverviewContent = updatedOverviewContent.replace(fallbackRegex, `$1$2${finalTreeText}\n$3`);
      console.log("[SUCCESS] Updated directory tree using heading fallback regex.");
    } else {
      console.error("[ERROR] Could not find any valid tree insertion target in overview.md.");
      process.exit(1);
    }
  }

  fs.writeFileSync(OVERVIEW_PATH, updatedOverviewContent, "utf8");
  console.log("=== Overview Update Complete ===");
}

main();
