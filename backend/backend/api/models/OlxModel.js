const {Sequelize, DataTypes} = require("sequelize");
const sequelize =require('./database').sequelize

const OlxModel = sequelize.define("olx", { 
    name: {
        type: DataTypes.STRING,
        allowNull: true
    },
    link: {
        type: DataTypes.STRING,
        allowNull: true
    },
    image: {
        type: DataTypes.STRING,
        allowNull: true
    },
    price: {
        type: DataTypes.STRING,
        allowNull: true
    },
    state: {
        type: DataTypes.STRING,
        allowNull: true
    },
    location: {
        type: DataTypes.STRING,
        allowNull: true
    }
},{
    tableName: 'olx',
    timestamps: false
});

module.exports=OlxModel;