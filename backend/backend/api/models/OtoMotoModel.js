const {Sequelize, DataTypes} = require("sequelize");
const sequelize =require('./database').sequelize

const OtoMotoModel = sequelize.define("oto_moto", { 
    create_date: {
        type: DataTypes.DATE(3), // Ensures millisecond precision and better handling
        allowNull: false,
        get() {
            return this.getDataValue('CreateDate')?.toISOString().slice(0, 19).replace('T', ' '); 
        }
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
    additional_info: {
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
    seller: {
        type: DataTypes.STRING,
        allowNull: true
    },
    location: {
        type: DataTypes.STRING,
        allowNull: true
    },
},{
    tableName: 'oto_moto',
    timestamps: false
});

module.exports=OtoMotoModel;