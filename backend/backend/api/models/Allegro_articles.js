const {Sequelize, DataTypes} = require("sequelize");
const sequelize =require('./database').sequelize

const Allegro_articles_models = sequelize.define("artykuly_allegro", { 
    product_name: {
        type: DataTypes.STRING,
        allowNull: true
    },
    image_link: {
        type: DataTypes.STRING,
        allowNull: true
    },
    has_promotion: {
        type: DataTypes.BOOLEAN,
        allowNull: true
    },
    quantity: {
        type: DataTypes.INTEGER,
        allowNull: true
    },
    price_in_PLN: {
        type: DataTypes.INTEGER,
        allowNull: true
    },
    popularity: {
        type: DataTypes.INTEGER,
        allowNull: true
    },
    delivery_in_PLN: {
        type: DataTypes.DECIMAL(10,2),
        allowNull: true
    },
    seller_name: {
        type: DataTypes.STRING,
        allowNull: true
    }
},{
    tableName: 'artykuly_allegro',
    timestamps: false
});

module.exports=Allegro_articles_models;