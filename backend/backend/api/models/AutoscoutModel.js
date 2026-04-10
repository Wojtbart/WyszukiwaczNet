const {Sequelize, DataTypes} = require("sequelize");
const sequelize =require('./database').sequelize

const AutoscoutModel = sequelize.define("autoscout", { 
    create_date: {
        type: DataTypes.DATE,
        allowNull: false
    },
    image: {
        type: DataTypes.STRING,
        allowNull: false
    },
    name: {
        type: DataTypes.STRING,
        allowNull: false
    },
    link: {
        type: DataTypes.STRING,
        allowNull: true
    },
    price: {
        type: DataTypes.STRING,
        allowNull: true
    },
    seller: {
        type: DataTypes.STRING,
        allowNull: true
    },
    mileage: {
        type: DataTypes.STRING,
        allowNull: true
    },
    kind_of_fuel: {
        type: DataTypes.STRING,
        allowNull: true
    },
    gearbox: {
        type: DataTypes.STRING,
        allowNull: true
    },
    production_year: {
        type: DataTypes.INTEGER,
        allowNull: true
    },
    motor_power: {
        type: DataTypes.STRING,
        allowNull: true
    },
},{
    tableName: 'autoscout',
    timestamps: false
});

module.exports=AutoscoutModel;