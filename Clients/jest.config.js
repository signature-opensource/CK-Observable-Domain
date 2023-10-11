module.exports = {
    moduleFileExtensions: ['js', 'json', 'ts'],
    rootDir: 'src',
    testRegex: '.*\\.spec\\.ts$',
    transform: {
        // Removes annoying ts-jest[config] (WARN) message TS151001: If you have issues related to imports, you should consider...
        '^.+\\.ts$': ['ts-jest', {diagnostics: {ignoreCodes: ['TS151001']}}],
    },
    testEnvironment: 'node',
    setupFiles: ["../jest.StObjTypeScriptEngine.js"]
};