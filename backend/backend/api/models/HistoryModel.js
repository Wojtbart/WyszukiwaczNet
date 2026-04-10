const {Sequelize, DataTypes} = require("sequelize");
const sequelize =require('./database').sequelize

const HistoryModel = sequelize.define("history", { 
    create_date: {
        type: DataTypes.DATE,
        allowNull: false
    },
    image: {
        type: DataTypes.STRING,
        allowNull: false
    },
    title: {
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
    }
},{
    tableName: 'history',
    timestamps: false
});

module.exports=HistoryModel;