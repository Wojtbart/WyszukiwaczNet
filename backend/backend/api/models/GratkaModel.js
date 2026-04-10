const {Sequelize, DataTypes} = require("sequelize");
const sequelize =require('./database').sequelize

const GratkaModel = sequelize.define("gratka", { 
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
    state: {
        type: DataTypes.STRING,
        allowNull: true
    },
    price: {
        type: DataTypes.STRING,
        allowNull: true
    },
    mileage: {
        type: DataTypes.STRING,
        allowNull: true
    },
    gearbox: {
        type: DataTypes.STRING,
        allowNull: true
    },
    ad_type: {
        type: DataTypes.STRING,
        allowNull: true
    },
    to_negotiations: {
        type: DataTypes.STRING,
        allowNull: true
    },
    address: {
        type: DataTypes.STRING,
        allowNull: true
    },
},{
    tableName: 'gratka',
    timestamps: false
});

module.exports=GratkaModel;