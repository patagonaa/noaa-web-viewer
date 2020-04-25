const path = require("path");
const HtmlWebpackPlugin = require("html-webpack-plugin");
const { CleanWebpackPlugin } = require('clean-webpack-plugin');
const CopyWebpackPlugin = require('copy-webpack-plugin');
const package = require('./package.json');

module.exports = {
    entry: {
        app: "./src/index.ts",
        projection: "./src/projection.ts",
        vendor: Object.keys(package.dependencies)
    },
    output: {
        path: path.resolve(__dirname, "wwwroot"),
        filename: "[name].js",
        publicPath: "/"
    },
    resolve: {
        extensions: [".js", ".ts"]
    },
    module: {
        rules: [
            {
                test: /\.ts$/,
                use: "ts-loader"
            },
            {
                test: /\.css$/,
                use: ['style-loader', 'css-loader']
            }
        ]
    },
    plugins: [
        new CleanWebpackPlugin(),
        new HtmlWebpackPlugin({
            template: "./src/index.html",
            filename: "index.html",
            chunks: ['vendor', 'app']
        }),
        new HtmlWebpackPlugin({
            template: "./src/projection.html",
            filename: "projection.html",
            chunks: ['vendor', 'projection']
        }),
        new CopyWebpackPlugin([
            { from: './src/icon.png' }
        ])
    ]
};