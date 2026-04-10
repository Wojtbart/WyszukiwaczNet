const {Sequelize, DataTypes} = require("sequelize");
const sequelize =require('./database').sequelize

const AutocentrumModel = sequelize.define("autocentrum", { 
    create_date: {
        type: DataTypes.DATE,
        allowNull: false
    },
    image: {
        type: DataTypes.STRING,
        allowNull: true
    },
    link: {
        type: DataTypes.STRING,
        allowNull: true
    },
    name: {
        type: DataTypes.STRING,
        allowNull: true
    },
    additional_info: {
        type: DataTypes.STRING,
        allowNull: true
    },
    address: {
        type: DataTypes.STRING,
        allowNull: true
    },
    price: {
        type: DataTypes.STRING,
        allowNull: true
    },
    production_year: {
        type: DataTypes.STRING,
        allowNull: true
    },
    engine: {
        type: DataTypes.STRING,
        allowNull: true
    },
    engine_power: {
        type: DataTypes.STRING,
        allowNull: true
    },
    gearbox: {
        type: DataTypes.STRING,
        allowNull: true
    },
    body_type: {
        type: DataTypes.STRING,
        allowNull: true
    },
    mileage: {
        type: DataTypes.STRING,
        allowNull: true
    }
}, {
    tableName: 'autocentrum',
    timestamps: false
});

module.exports = AutocentrumModel;