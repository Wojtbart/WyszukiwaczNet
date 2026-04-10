const {Sequelize, DataTypes} = require("sequelize");
const sequelize =require('./database').sequelize

const SprzedajemyModel = sequelize.define("sprzedajemy", { 
    create_date: {
        type: DataTypes.DATE,
        allowNull: false
    },
    image: {
        type: DataTypes.STRING,
        allowNull: false
    },
    added_date: {
        type: DataTypes.STRING,
        allowNull: false
    },
    address: {
        type: DataTypes.STRING,
        allowNull: true
    },
    name: {
        type: DataTypes.STRING,
        allowNull: true
    },
    link: {
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
    gearbox: {
        type: DataTypes.STRING,
        allowNull: true
    },
    motor_engine: {
        type: DataTypes.STRING,
        allowNull: true
    }
},{
    tableName: 'sprzedajemy',
    timestamps: false
});

module.exports=SprzedajemyModel;