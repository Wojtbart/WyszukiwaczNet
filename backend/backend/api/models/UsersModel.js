const {Sequelize, DataTypes} = require("sequelize");
const sequelize =require('./database').sequelize;

const UsersModel = sequelize.define("users", { 
    name: {
        type: DataTypes.STRING,
        allowNull: false
    },
    surname: {
        type: DataTypes.STRING,
        allowNull: false
    },
    email: {
        type: DataTypes.STRING,
        allowNull: true
    },
    login: {
        type: DataTypes.STRING,
        allowNull: true
    },
    password: {
        type: DataTypes.STRING,
        allowNull: false
    },
    phone: {
        type: DataTypes.STRING,
        allowNull: false
    },
},{
    tableName: 'users',
    timestamps: false
});

const Users_configuration_model = sequelize.define("user_configuration", { 
    olx: {
        type: DataTypes.BOOLEAN,
        allowNull: true
    },
    amazon: {
        type: DataTypes.BOOLEAN,
        allowNull: true
    },
    allegro: {
        type: DataTypes.BOOLEAN,
        allowNull: true
    },
    pepper: {
        type: DataTypes.BOOLEAN,
        allowNull: true
    },
    oto_moto: {
        type: DataTypes.BOOLEAN,
        allowNull: true
    },
    sprzedajemy: {
        type: DataTypes.BOOLEAN,
        allowNull: true
    },
    oto_dom: {
        type: DataTypes.BOOLEAN,
        allowNull: true
    },
    sms: {
        type: DataTypes.BOOLEAN,
        allowNull: true
    },
    discord: {
        type: DataTypes.BOOLEAN,
        allowNull: true
    },
    email: {
        type: DataTypes.BOOLEAN,
        allowNull: true
    },
    godzina_maila: {
        type: DataTypes.STRING,
        allowNull: true
    },
    repeat_after_specified_time: {
        type: DataTypes.INTEGER,
        allowNull: true
    },
    user_id: {
        type: DataTypes.INTEGER,
        allowNull: true
    } 
},{
    tableName: 'user_configuration',
    timestamps: false
});
UsersModel.hasOne(Users_configuration_model,{foreignKey: {
    name: 'user_id'
}});

// sequelize.query(' DROP TRIGGER IF EXISTS artykuly.after_user_create_save_user_empty_config;')

// sequelize.query(
// ' CREATE TRIGGER after_user_create_save_user_empty_config AFTER INSERT ON users' +
// ' FOR EACH ROW' +
// ' BEGIN' +
// ' DECLARE userId int;' +
// ' Select(SELECT id from users order by id desc limit 1) into userId;' +
// ' INSERT INTO user_configuration(olx, amazon, allegro, pepper, sms, discord, email, aktualna_oferta, godzina_maila, repeat_after_specified_time, user_id) VALUES( false, false,false,false,false, false, false, false, null, 0, userId);' +
// 'END;')

module.exports={UsersModel,Users_configuration_model};