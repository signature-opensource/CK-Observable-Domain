const path = require('path');

module.exports = {
    mode: 'production',
    entry: './src/index.ts',
    devtool: 'source-map',
    output: {
        path: path.resolve(__dirname, 'dist'),
        filename: 'ck-observable-domain.js',
        library: 'ckObservableDomain',
        libraryTarget: 'umd',
        globalObject: 'this',
    },
    module: {
        rules: [
            {
                test: /\.ts$/,
                exclude: /node_modules/,
                use: {
                    loader: "awesome-typescript-loader"
                }
            }
        ]
    }
};