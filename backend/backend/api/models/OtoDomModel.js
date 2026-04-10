const { Sequelize, DataTypes } = require("sequelize");
const sequelize = require("./database").sequelize;

const OtoDomModel = sequelize.define("oto_dom", {
    create_date: {
        type: DataTypes.DATE(3),
        allowNull: false,
        get() {
            return this.getDataValue('CreateDate')?.toISOString().slice(0, 19).replace('T', ' '); 
        }
    },
    title: {
        type: DataTypes.STRING,
        allowNull: false
    },
    price: {
        type: DataTypes.STRING,
        allowNull: true
    },
    location: {
        type: DataTypes.STRING,
        allowNull: true
    },
    link: {
        type: DataTypes.STRING,
        allowNull: false,
        unique: true
    },
    image: {
        type: DataTypes.STRING,
        allowNull: true
    },
    additional_info: {
        type: DataTypes.TEXT,
        allowNull: true
    }
}, {
    tableName: "oto_dom",
    timestamps: false
});

module.exports = OtoDomModel;
