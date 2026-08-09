import fs from 'node:fs';
import path from 'node:path';
import ts from 'typescript';

const projectRoot = process.cwd();
const configPath = ts.findConfigFile(projectRoot, ts.sys.fileExists, 'tsconfig.json');
if (!configPath) throw new Error('tsconfig.json was not found');

const configFile = ts.readConfigFile(configPath, ts.sys.readFile);
const parsedConfig = ts.parseJsonConfigFileContent(configFile.config, ts.sys, projectRoot);
const program = ts.createProgram(parsedConfig.fileNames, parsedConfig.options);
const checker = program.getTypeChecker();
const shouldFix = process.argv.includes('--fix');
const violations = [];

function isDateReceiver(expression) {
  const receiverType = checker.getTypeAtLocation(expression);
  return checker.typeToString(receiverType).replaceAll('null', '').replaceAll('undefined', '').includes('Date');
}

function localeOptionsIndex(call) {
  if (ts.isPropertyAccessExpression(call.expression)) {
    const method = call.expression.name.text;
    if (['toLocaleString', 'toLocaleDateString', 'toLocaleTimeString'].includes(method)
      && isDateReceiver(call.expression.expression)) return 1;
  }

  if ((ts.isNewExpression(call) || ts.isCallExpression(call))
    && ts.isPropertyAccessExpression(call.expression)
    && ts.isIdentifier(call.expression.expression)
    && call.expression.expression.text === 'Intl'
    && call.expression.name.text === 'DateTimeFormat') return 1;

  return null;
}

function hasExplicitTimeZone(options) {
  return ts.isObjectLiteralExpression(options) && options.properties.some((property) =>
    ts.isPropertyAssignment(property)
    && ((ts.isIdentifier(property.name) && property.name.text === 'timeZone') || (ts.isStringLiteral(property.name) && property.name.text === 'timeZone'))
    && ts.isStringLiteral(property.initializer));
}

function editsForCall(sourceFile, call, optionsIndex) {
  const args = call.arguments ?? [];
  if (args.length <= optionsIndex) {
    const position = args.length === 0 ? call.expression.end + 1 : args[args.length - 1].end;
    const text = args.length === 0 ? "undefined, { timeZone: 'Africa/Cairo' }" : ", { timeZone: 'Africa/Cairo' }";
    return [{ position, text }];
  }

  const options = args[optionsIndex];
  if (hasExplicitTimeZone(options)) return [];
  if (ts.isObjectLiteralExpression(options)) return [{ position: options.getStart(sourceFile) + 1, text: " timeZone: 'Africa/Cairo'," }];
  return null;
}

for (const sourceFile of program.getSourceFiles()) {
  if (!sourceFile.fileName.startsWith(path.join(projectRoot, 'src')) || sourceFile.isDeclarationFile) continue;
  const edits = [];

  function visit(node) {
    if (ts.isCallExpression(node) || ts.isNewExpression(node)) {
      const optionsIndex = localeOptionsIndex(node);
      if (optionsIndex !== null) {
        const requiredEdits = editsForCall(sourceFile, node, optionsIndex);
        if (requiredEdits === null) {
          const location = sourceFile.getLineAndCharacterOfPosition(node.getStart(sourceFile));
          violations.push(`${path.relative(projectRoot, sourceFile.fileName)}:${location.line + 1}: options must declare Africa/Cairo`);
        } else {
          edits.push(...requiredEdits);
        }
      }
    }
    ts.forEachChild(node, visit);
  }

  visit(sourceFile);
  if (edits.length === 0) continue;
  const relativePath = path.relative(projectRoot, sourceFile.fileName);
  if (!shouldFix) {
    violations.push(`${relativePath}: missing Africa/Cairo in ${edits.length} date formatter(s)`);
    continue;
  }

  let source = sourceFile.getFullText();
  for (const edit of edits.sort((left, right) => right.position - left.position)) {
    source = source.slice(0, edit.position) + edit.text + source.slice(edit.position);
  }
  fs.writeFileSync(sourceFile.fileName, source);
}

if (violations.length > 0) {
  console.error(violations.join('\n'));
  process.exitCode = 1;
}
